"""Fixed edge cases independent of random training generator; Unity parity fixtures."""
import json
from pathlib import Path
import numpy as np
import onnxruntime as ort
from stroke_dataset import rasterize, CLASS_NAMES


def main():
    root=Path('Logs/caret-training-20261021')
    angle=np.linspace(0,2*np.pi,81)
    circle=np.column_stack([100+80*np.cos(angle),100+90*np.sin(angle)])
    paths={
        'caret_sharp':(1,[[(0,0),(50,100),(100,0)]]),
        'caret_tall':(1,[[(0,0),(35,100),(70,0)]]),
        'caret_asymmetric':(1,[[(0,0),(20,17),(42,39),(65,65),(85,80),(93,64),(96,32),(100,4)]]),
        'caret_curved':(1,[[(0,0),(8,30),(19,56),(34,78),(49,90),(66,100),(70,81),(79,50),(95,0)]]),
        'caret_two_strokes':(1,[[(0,0),(50,100)],[(100,0),(50,100)]]),
        'V':(0,[[(0,100),(50,0),(100,100)]]),
        'W':(6,[[(0,100),(25,0),(50,65),(75,0),(100,100)]]),
        'O':(4,[circle]),
        'E':(7,[[(100,100),(0,100),(0,0),(100,0)],[(0,50),(70,50)]]),
        'E_missing_middle':(10,[[(100,100),(0,100),(0,0),(100,0)]]),
    }
    sessions={name:ort.InferenceSession(str(root/file),providers=['CPUExecutionProvider'])
              for name,file in [('before','original.onnx'),('after','candidate.onnx')]}
    fixtures=[]
    for name,(label,strokes) in paths.items():
        strokes=[np.array(s,np.float32) for s in strokes]
        pixels=rasterize(strokes)
        predictions={}
        for version,session in sessions.items():
            z=session.run(None,{'input':pixels[None,None]})[0][0]
            p=np.exp(z-z.max());p/=p.sum()
            predictions[version]={'top':CLASS_NAMES[p.argmax()],'score':float(p.max()),'caret':float(p[1])}
            if version=='after': expected=z.tolist()
        pts=[{'X':float(p[0]),'Y':float(p[1]),'StrokeID':sid} for sid,s in enumerate(strokes) for p in s]
        fixtures.append({'name':name,'label':label,'points':pts,'pixels':pixels.flatten().tolist(),'logits':expected})
        print(name,json.dumps(predictions),flush=True)
    (root/'unity-fixtures.json').write_text(json.dumps({'cases':fixtures}),encoding='utf-8')


if __name__=='__main__': main()
