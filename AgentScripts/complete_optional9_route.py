from pathlib import Path
p=Path('Verification/feedback-221708/routes/9.plan');s=p.read_text();s=s.replace('P 15 14\nW 5 7','''P 15 14
Buy 7
Buy 6
Buy 6
W 9 11
Face 1 0
U 7 10 11 1 0
W 11 11
P 12 11
W 14 12
U 6 13 11 0 1
W 13 11
P 13 12
W 13 12
P 13 13
W 14 13
U 6 13 14 1 0
W 13 14
P 14 14
W 5 7''');s=s.replace('W 14 14\nW 14 15\nW 15 15','W 14 14\nW 15 14\nW 15 15');p.write_text(s)
