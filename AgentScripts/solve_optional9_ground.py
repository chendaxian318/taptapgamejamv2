from pathlib import Path
s=Path('Verification/final-acceptance-20261018/routes/9.plan').read_text(encoding='utf-8-sig');s=s[:s.index('Take 0\nW 7 16')].replace('Buy 3\n','')+'''W 7 16
W 8 16
W 9 16
W 6 13
P 6 14
W 6 14
P 6 15
W 5 16
P 6 16
# A single floating step east of the crab, made from the existing eastern box.
W 14 12
P 14 13
W 13 14
P 14 14
W 15 13
P 15 14
W 5 7
I 6 7
W 10 14
W 14 15
W 15 15
W 14 15
W 13 15
W 12 15
W 11 15
''';Path('Verification/feedback-221708/routes/9.plan').write_text(s)
