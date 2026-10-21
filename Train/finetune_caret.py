"""Fine-tune existing fused MobileMagicCNN ONNX; never overwrite the game asset.

Usage: .venv/Scripts/python Train/finetune_caret.py --output Logs/caret-training
Select checkpoints on validation only, then report untouched holdout results.
"""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import shutil
import numpy as np
import onnx
from onnx import numpy_helper
import onnxruntime as ort
import torch
from torch import nn
from torch.utils.data import DataLoader, TensorDataset
from stroke_dataset import CLASS_NAMES, make_dataset


class FusedMagicCNN(nn.Module):
    def __init__(self, source):
        super().__init__()
        self.features = nn.Sequential(nn.Conv2d(1,16,3,padding=1), nn.ReLU(), nn.MaxPool2d(2),
            nn.Conv2d(16,32,3,padding=1), nn.ReLU(), nn.MaxPool2d(2),
            nn.Conv2d(32,64,3,padding=1), nn.ReLU(), nn.MaxPool2d(2))
        self.classifier = nn.Sequential(nn.Flatten(), nn.Linear(576,128), nn.ReLU(), nn.Linear(128,11))
        arrays = {t.name:numpy_helper.to_array(t).copy() for t in source.graph.initializer}
        nodes = [n for n in source.graph.node if n.op_type in ('Conv','Gemm')]
        layers = [self.features[0],self.features[3],self.features[6],self.classifier[1],self.classifier[3]]
        if len(nodes)!=len(layers):
            raise ValueError('Unsupported ONNX architecture; expected 3 Conv and 2 Gemm.')
        with torch.no_grad():
            for node, layer in zip(nodes,layers):
                layer.weight.copy_(torch.from_numpy(arrays[node.input[1]]))
                layer.bias.copy_(torch.from_numpy(arrays[node.input[2]]))

    def forward(self,x):
        return self.classifier(self.features(x))


def logits_for(model,x):
    model.eval()
    with torch.no_grad():
        return torch.cat([model(torch.from_numpy(x[i:i+256])) for i in range(0,len(x),256)]).numpy()


def metrics(logits,y):
    p=np.exp(logits-logits.max(1,keepdims=True)); p/=p.sum(1,keepdims=True)
    pred=p.argmax(1)
    ordered=np.sort(p,axis=1)
    accepted=(ordered[:,-1]>=.85)&((ordered[:,-1]-ordered[:,-2])>=.30)&(pred!=10)
    matrix=np.zeros((11,11),int)
    np.add.at(matrix,(y,pred),1)
    classes={}
    for cid,name in enumerate(CLASS_NAMES):
        mask=y==cid
        classes[name]={'top1_accuracy':float((pred[mask]==cid).mean()),
            'correct_accepted':float(((pred==cid)&accepted)[mask].mean()),
            'wrong_accepted':float(((pred!=cid)&accepted)[mask].mean())}
    return {'accuracy':float((pred==y).mean()),'classes':classes,'confusion_matrix':matrix.tolist()}


def export_weights(source, model, destination):
    # Preserve original operator graph/opset, replacing only trained tensors.
    result=copy.deepcopy(source)
    nodes=[n for n in result.graph.node if n.op_type in ('Conv','Gemm')]
    layers=[model.features[0],model.features[3],model.features[6],model.classifier[1],model.classifier[3]]
    tensors={}
    for node,layer in zip(nodes,layers):
        tensors[node.input[1]]=layer.weight.detach().numpy()
        tensors[node.input[2]]=layer.bias.detach().numpy()
    for initial in result.graph.initializer:
        if initial.name in tensors:
            initial.CopyFrom(numpy_helper.from_array(tensors[initial.name],initial.name))
    onnx.checker.check_model(result)
    onnx.save(result,str(destination))


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--source',default='Assets/Models/magic_touch_model.onnx')
    parser.add_argument('--output',default='Logs/caret-training')
    parser.add_argument('--per-class',type=int,default=1000)
    parser.add_argument('--epochs',type=int,default=12)
    args=parser.parse_args()
    torch.set_num_threads(4)
    torch.manual_seed(20261021)
    out=Path(args.output); out.mkdir(parents=True,exist_ok=True)
    backup=out/'original.onnx'
    if backup.exists() and backup.read_bytes()!=Path(args.source).read_bytes():
        raise RuntimeError('Output directory contains a different baseline. Choose a new output directory.')
    if not backup.exists(): shutil.copy2(args.source,backup)
    source=onnx.load(args.source)
    model=FusedMagicCNN(source)
    teacher=copy.deepcopy(model).eval()
    for p in teacher.parameters(): p.requires_grad_(False)
    sets={}
    for name,count,seed,stress in [('train',args.per_class,1201,False),('val',180,2302,False),('test',300,3403,True)]:
        path=out/(name+'.npz')
        if path.exists():
            data=np.load(path); x,y=data['x'],data['y']
        else:
            x,y=make_dataset(count,seed,stress, varied_brush=name!='test')
            np.savez_compressed(path,x=x,y=y)
        sets[name]=(x,y)
    options=ort.SessionOptions(); options.intra_op_num_threads=4
    original=ort.InferenceSession(str(backup),sess_options=options,providers=['CPUExecutionProvider'])
    parity_x=sets['val'][0][:40]
    expected=original.run(None,{'input':parity_x})[0]
    error=float(np.max(np.abs(expected-logits_for(model,parity_x))))
    if error>1e-3: raise RuntimeError(f'Original model import parity failed: {error}')
    print('Original ONNX/PyTorch max error:',error,flush=True)
    base_val=metrics(logits_for(teacher,sets['val'][0]),sets['val'][1])
    baseline=metrics(logits_for(teacher,sets['test'][0]),sets['test'][1])
    print('Baseline holdout:',json.dumps(baseline['classes']),flush=True)
    train_x,train_y=sets['train']
    loader=DataLoader(TensorDataset(torch.from_numpy(train_x),torch.from_numpy(train_y)),batch_size=128,shuffle=True)
    optimizer=torch.optim.Adam(model.parameters(),lr=1e-4,weight_decay=1e-5)
    reference={name:p.detach().clone() for name,p in teacher.named_parameters()}
    best=-1; history=[]
    for epoch in range(args.epochs):
        model.train(); total=0
        for x,y in loader:
            z=model(x)
            loss=nn.functional.cross_entropy(z,y)
            # Preserve teacher behavior only when it agrees with the known label.
            with torch.no_grad(): old=teacher(x); keep=(old.argmax(1)==y)&(y!=1)
            if keep.any():
                loss += .25*nn.functional.kl_div(nn.functional.log_softmax(z[keep]/2,dim=1),
                    nn.functional.softmax(old[keep]/2,dim=1),reduction='batchmean')*4
            loss += .01*sum((p-reference[name]).square().mean() for name,p in model.named_parameters())
            optimizer.zero_grad(); loss.backward(); optimizer.step(); total+=float(loss.detach())
        val=metrics(logits_for(model,sets['val'][0]),sets['val'][1])
        history.append({'epoch':epoch+1,'loss':total/len(loader),'validation':val})
        c=val['classes']['Caret']; print(f'Epoch {epoch+1}: val={val["accuracy"]:.4f}, caret accepted={c["correct_accepted"]:.4f}',flush=True)
        # Select by overall validation accuracy and caret acceptance, never test accuracy.
        score=val['accuracy']+.3*c['correct_accepted']
        if score>best:
            best=score; torch.save(model.state_dict(),out/'best.pth')
    model.load_state_dict(torch.load(out/'best.pth',weights_only=True))
    candidate=out/'candidate.onnx'; export_weights(source,model,candidate)
    session=ort.InferenceSession(str(candidate),sess_options=options,providers=['CPUExecutionProvider'])
    test_x,test_y=sets['test']; new_logits=session.run(None,{'input':test_x})[0]
    export_error=float(np.max(np.abs(session.run(None,{'input':parity_x})[0]-logits_for(model,parity_x))))
    if export_error>1e-3: raise RuntimeError(f'Export parity failed: {export_error}')
    improved=metrics(new_logits,test_y)
    regressions=[name for name in CLASS_NAMES if name not in ('Caret','Unknown') and
        improved['classes'][name]['correct_accepted'] < baseline['classes'][name]['correct_accepted']-.03]
    eligible=(improved['classes']['Caret']['correct_accepted']>=baseline['classes']['Caret']['correct_accepted']+.10
        and improved['accuracy']>=baseline['accuracy'] and not regressions
        and improved['classes']['Unknown']['wrong_accepted']<=baseline['classes']['Unknown']['wrong_accepted']+.02)
    report={'source_sha256':hashlib.sha256(backup.read_bytes()).hexdigest(),
        'candidate_sha256':hashlib.sha256(candidate.read_bytes()).hexdigest(),
        'dataset':'Synthetic screen-space strokes; seeds train=1201 val=2302 test=3403; test uses wider shape variation.',
        'limitations':'No held-out human handwriting set; synthetic results do not guarantee real-player accuracy.',
        'class_names':CLASS_NAMES,'baseline':baseline,'candidate':improved,'history':history,
        'import_max_error':error,'export_max_error':export_error,'regressions':regressions,'eligible_for_trial':eligible}
    (out/'report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print('Candidate holdout:',json.dumps(improved['classes']),flush=True)
    print('Eligible for trial:',eligible,'regressions:',regressions,flush=True)


if __name__=='__main__': main()
