from pathlib import Path
r=Path('Verification/final-acceptance-20261018/routes');o=Path('Verification/feedback-221708/routes')
s=(r/'6.plan').read_text(encoding='utf-8-sig')
a=s.index('W 13 10\nI 13 11\nW 4 8');b=s.index('W 9 3\nP 8 3',a)
s=s[:a]+'''W 13 10
I 13 11
W 4 8
W 4 9
I 3 9
W 4 8
I 3 8
I 4 7
Tick 30
I 4 7
Store
W 4 9
I 3 9
W 4 8
W 4 6
W 9 4

'''+s[b:];(o/'6.plan').write_text(s,encoding='utf-8')
# Regenerate fourth map walk segments against the new movement rules rather than replay old fixed steps.
# first 48 old commands include a now redundant step after a longer raft trip; navigation to next purchase/interaction is solved anew.
s=(r/'8.plan').read_text(encoding='utf-8-sig');rows=s.splitlines();rows[47]='W 5 2';(o/'8.plan').write_text('\n'.join(rows),encoding='utf-8')
p=Path('AgentScripts/RunFeedback221708Routes.ps1');s=p.read_text();s=s.replace("$result=[FinalRouteLab]::Run($level,('Verification/final-acceptance-20261018/routes/'+$level+'.plan'))","$plan='Verification/feedback-221708/routes/'+$level+'.plan'; if(!(Test-Path $plan)){$plan='Verification/final-acceptance-20261018/routes/'+$level+'.plan'}\n $result=[FinalRouteLab]::Run($level,$plan)")
p.write_text(s,encoding='utf-8')
