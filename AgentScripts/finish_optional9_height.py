from pathlib import Path
p=Path('Verification/feedback-221708/routes/9.plan');s=p.read_text();s=s.replace('W 9 11\nFace 1 0','''Buy 5
Buy 5
W 8 12
Face 1 0
U 5 9 12 1 0
W 9 12
Face 0 -1
U 5 9 11 0 -1
W 9 11
Face 1 0''');s=s.replace('W 11 11\nP 12 11','Buy 6\nW 13 12\nU 6 12 11 1 0');p.write_text(s)
