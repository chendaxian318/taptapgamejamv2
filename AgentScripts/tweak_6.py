from pathlib import Path
p=Path('Verification/feedback-221708/routes/6.plan');s=p.read_text();s=s.replace('W 13 10\nI 13 11\nW 4 8\nW 4 9','W 4 8\nW 4 9');s=s.replace('W 4 6\nW 9 4\n','W 4 6\nW 9 4\nI 9 3\n');s=s.replace('W 9 9\nI 8 9\nW 9 4\nI 9 3\n','');p.write_text(s)
