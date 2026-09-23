from pathlib import Path
import math,struct,zlib
out=Path(__file__).resolve().parent / 'Assets';out.mkdir(exist_ok=True)
def rr(x,y,l,t,r,b,k):
    cx=max(l+k,min(x,r-k));cy=max(t+k,min(y,b-k))
    return l<=x<=r and t<=y<=b and (x-cx)**2+(y-cy)**2<=k*k
def color(x,y):
    c=(0,0,0,0)
    for bounds,paint in [((1,1,63,63,14),(20,43,64,255)),((10,12,52,51,6),(240,247,252,255)),((10,12,52,25,5),(34,187,178,255)),((16,30,29,36,2),(79,146,202,255)),((16,40,29,46,2),(79,146,202,255)),((33,30,46,36,2),(255,185,84,255)),((19,7,23,18,2),(240,247,252,255)),((39,7,43,18,2),(240,247,252,255))]:
        if rr(x,y,*bounds):c=paint
    if (x-47)**2+(y-47)**2<=14**2:c=(20,43,64,255)
    if (x-47)**2+(y-47)**2<=11**2:c=(255,255,255,255)
    if rr(x,y,45.6,39,48.4,48.4,1.3) or rr(x,y,46,46,54,48.8,1.3):c=(20,43,64,255)
    return c
def chunk(t,b):return struct.pack('>I',len(b))+t+b+struct.pack('>I',zlib.crc32(t+b)&0xffffffff)
def png(n):
    raw=bytearray()
    for y in range(n):
        raw.append(0)
        for x in range(n):
            pixels=[color((x+(i+.5)/3)*64/n,(y+(j+.5)/3)*64/n) for i in range(3) for j in range(3)]
            raw.extend(round(sum(p[k] for p in pixels)/9) for k in range(4))
    return b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',n,n,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(raw))+chunk(b'IEND',b'')
sizes=[16,24,32,48,64,128,256];images=[png(n) for n in sizes];offset=6+16*len(sizes);data=struct.pack('<HHH',0,1,len(sizes))
for n,p in zip(sizes,images):data+=struct.pack('<BBBBHHII',n%256,n%256,0,0,1,32,len(p),offset);offset+=len(p)
(out/'KimaiBlocks.ico').write_bytes(data+b''.join(images));(out/'KimaiBlocks.png').write_bytes(images[-1])
