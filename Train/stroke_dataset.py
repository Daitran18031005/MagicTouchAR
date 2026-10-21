"""Deterministic screen-space strokes and Unity-compatible 28x28 rasterization.

No image-space warps after rasterizing: the same centering, padding and quadratic
brush as GesturePreprocessor.cs are used. Seeds identify disjoint generated sets.
"""
import numpy as np

CLASS_NAMES = ['V', 'Caret', 'HorizontalLine', 'VerticalLine', 'O', 'Z', 'W', 'E', 'X', 'S', 'Unknown']


def rasterize(strokes, radius=1.3):
    points = np.concatenate(strokes).astype(np.float32)
    low, high = points.min(0), points.max(0)
    scale = np.float32(20.0 / max(float((high-low).max()), 1.0))
    center = (low+high)*np.float32(.5)
    grid = np.zeros((28, 28), np.float32)
    for stroke in strokes:
        q = (np.asarray(stroke, np.float32)-center)*scale
        q[:, 0] += 14
        q[:, 1] = 14-q[:, 1]
        stamps = [q[:1]]
        for a, b in zip(q[:-1], q[1:]):
            steps = max(1, int(np.ceil(np.linalg.norm(b-a)*2.5)))
            t = np.linspace(0, 1, steps+1, dtype=np.float32)[:, None]
            stamps.append(a+(b-a)*t)
        stamps = np.concatenate(stamps)
        axis = np.arange(28, dtype=np.float32)
        dx = (axis[None,:]-stamps[:,0,None])**2
        dy = (axis[None,:]-stamps[:,1,None])**2
        distances = dy[:,:,None]+dx[:,None,:]
        alpha = np.maximum(0, 1-distances.min(0)/np.float32(radius*radius))
        grid = np.maximum(grid, alpha)
    return grid


def curve(a, b, bow, steps=14):
    a, b = np.array(a, float), np.array(b, float)
    t = np.linspace(0, 1, steps)[:, None]
    control = (a+b)/2 + np.array(bow)
    return (1-t)**2*a + 2*(1-t)*t*control + t*t*b


def poly(vertices, rng, bend=.04):
    return np.concatenate([curve(a, b, rng.uniform(-bend, bend, 2)) for a, b in zip(vertices[:-1], vertices[1:])])


def generate_strokes(cid, rng, stress=False):
    """Coordinates are Y-up, as in game. Shape diversity precedes rasterization."""
    if cid in (0, 1):
        apex = rng.uniform(.18, .82) if stress else rng.uniform(.25, .75)
        bend = rng.uniform(0, .30 if stress else .23)
        left = curve((0, rng.uniform(0, .12)), (apex, 1), (-bend, bend))
        right = curve((apex, 1), (1, rng.uniform(0, .12)), (bend, bend))
        s = np.concatenate([left, right])
        if cid == 0:
            s[:, 1] = 1-s[:, 1]
        strokes = [s]
    elif cid == 2:
        strokes = [curve((0, .5), (1, .5+rng.uniform(-.03, .03)), (0, rng.uniform(-.02, .02)))]
    elif cid == 3:
        strokes = [curve((.5, 0), (.5+rng.uniform(-.03, .03), 1), (rng.uniform(-.02, .02), 0))]
    elif cid == 4:
        start = rng.uniform(0, 2*np.pi)
        theta = np.linspace(start, start+2*np.pi-rng.uniform(0, .18), 65)
        strokes = [np.column_stack([.5+.5*np.cos(theta), .5+.5*np.sin(theta)])]
    elif cid == 5:
        strokes = [poly([(0,1),(1,1),(0,0),(1,0)], rng)]
    elif cid == 6:
        strokes = [poly([(0,1),(.23,0),(.5,rng.uniform(.55,.9)),(.77,0),(1,1)], rng)]
    elif cid == 7:
        strokes = [poly([(1,1),(0,1),(0,0),(1,0)], rng), poly([(0,.5),(rng.uniform(.55,.9),.5)], rng)]
    elif cid == 8:
        strokes = [poly([(0,1),(1,0)], rng), poly([(0,0),(1,1)], rng)]
    elif cid == 9:
        # Two smoothly joined lobes rather than a squared-off zigzag.
        top = curve((.95,.9), (.12,.65), (-.3,.5))
        mid = curve((.12,.65), (.88,.35), (0,0))
        bot = curve((.88,.35), (.05,.1), (.3,-.5))
        strokes = [np.concatenate([top,mid,bot])]
    else:
        mode = int(rng.integers(0,5))
        if mode == 0: # Incomplete E (no middle) is not a valid character.
            strokes = [poly([(1,1),(0,1),(0,0),(1,0)], rng)]
        elif mode == 1: # Sideways/open-bottom loops, not upper caret-like arcs.
            theta = np.linspace(-np.pi*.6, np.pi*.6, 45)
            strokes = [np.column_stack([np.cos(theta),np.sin(theta)])]
        elif mode == 2:
            strokes = [poly([(0,0),(.5,1),(1,0),(0,0)], rng)]
        elif mode == 3:
            strokes = [poly([(0,0),(0,1),(1,1),(1,0),(0,0)], rng)]
        else:
            strokes = [poly(rng.uniform(0,1,(int(rng.integers(5,9)),2)), rng)]
    # Scale, modest tilt, low-frequency drift and sampling variation.
    angle = rng.uniform(-12,12)*np.pi/180
    rot = np.array([[np.cos(angle),-np.sin(angle)],[np.sin(angle),np.cos(angle)]])
    sx = rng.uniform(.55,1.4) if cid not in (2,3) else 1
    sy = rng.uniform(.7,1.5) if cid not in (2,3) else 1
    output = []
    for s in strokes:
        q = (np.asarray(s)-.5)*[sx,sy]
        q = q @ rot.T
        q += rng.normal(0,.0025,q.shape)
        if rng.random()<.5:
            q = q[::-1].copy()
        output.append((q*180+250).astype(np.float32))
    rng.shuffle(output)
    return output


def make_dataset(per_class, seed, stress=False, varied_brush=True):
    rng = np.random.default_rng(seed)
    images, labels = [], []
    for cid in range(len(CLASS_NAMES)):
        for _ in range(per_class):
            radius = rng.uniform(1.05,1.6) if varied_brush else 1.3
            images.append(rasterize(generate_strokes(cid,rng,stress),radius))
            labels.append(cid)
        print(f'generated {CLASS_NAMES[cid]}: {per_class}', flush=True)
    return np.asarray(images,np.float32)[:,None], np.asarray(labels,np.int64)
