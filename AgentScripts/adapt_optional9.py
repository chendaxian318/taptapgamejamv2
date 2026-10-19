from pathlib import Path
p=Path('Verification/final-acceptance-20261018/routes/9.plan');s=p.read_text(encoding='utf-8-sig');a=s.index('Take 0\nW 7 16');b=s.index('W 5 7',a);s=s[:a]+s[b:];Path('Verification/feedback-221708/routes/9.plan').write_text(s)
