from pathlib import Path
import zipfile, json, hashlib, re
from PIL import Image, ImageDraw, ImageFont
root=Path(__file__).resolve().parents[1]
source=Path(r'D:/software/social/jl/xwechat_files/wxid_8mef8sxck69x22_553c/msg/file/2026-10/ui.zip')
dest=root/'Docs/SourceEvidence/UI_Final_20261018'
out=root/'Verification/ui-final-20261018'
dest.mkdir(parents=True,exist_ok=True);out.mkdir(parents=True,exist_ok=True)
inventory=[]
with zipfile.ZipFile(source) as z:
    for entry in z.infolist():
        if entry.is_dir():continue
        target=(dest/entry.filename).resolve()
        assert target.is_relative_to(dest.resolve())
        target.parent.mkdir(parents=True,exist_ok=True)
        data=z.read(entry)
        if target.exists():assert target.read_bytes()==data
        else:target.write_bytes(data)
        record={'path':entry.filename,'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()}
        if target.suffix.lower()=='.png':
            with Image.open(target) as im:
                rgba=im.convert('RGBA');alpha=rgba.getchannel('A')
                record.update(width=im.width,height=im.height,alpha=alpha.getextrema(),bounds=alpha.getbbox())
        inventory.append(record)
(out/'inventory.json').write_text(json.dumps(inventory,ensure_ascii=False,indent=2),encoding='utf-8')
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',16)
for group in sorted({p['path'].split('/')[0] for p in inventory}):
    rows=[p for p in inventory if p['path'].startswith(group+'/') and p['path'].endswith('.png')]
    cols=4;cw,ch=350,235
    canvas=Image.new('RGB',(cols*cw,((len(rows)+cols-1)//cols)*ch),'#c7d6dc');draw=ImageDraw.Draw(canvas)
    for index,r in enumerate(rows):
        x,y=(index%cols)*cw,(index//cols)*ch
        with Image.open(dest/r['path']) as im:
            im=im.convert('RGBA');im.thumbnail((cw-16,ch-55))
            canvas.paste(im,(x+(cw-im.width)//2,y+5+(ch-55-im.height)//2),im)
        label=r['path'].split('/')[-1].replace('@300x','')
        if '鼠标' in r['path']:label='悬停 '+label
        if '通关按钮' in r['path']:label='通关 '+label
        draw.text((x+5,y+ch-45),label[:23],font=font,fill='#182d38')
        draw.text((x+5,y+ch-23),f'{r["width"]}x{r["height"]} alpha:{r["alpha"]}',font=font,fill='#182d38')
    canvas.save(out/(group[:2]+'-contact.jpg'),quality=92)
print(json.dumps({'files':len(inventory),'pngs':sum('width' in x for x in inventory),'sourceBytes':source.stat().st_size,'groups':sorted({p['path'].split('/')[0] for p in inventory})},ensure_ascii=True))
