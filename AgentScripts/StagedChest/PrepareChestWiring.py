"""Prepare reviewed full sources and a diff without touching Assets.
Run with --apply ONLY after the parent agent has authorized the Unity refresh window.
"""
from pathlib import Path
import argparse
import datetime
import difflib
import shutil

stage = Path(__file__).resolve().parent
project = stage.parent.parent
scripts = project / 'Assets/HabitatIsland/Scripts'
parser = argparse.ArgumentParser()
parser.add_argument('--apply', action='store_true')
args = parser.parse_args()

game_edits = [
    ('            TickUtilityPresentation();', '            TickUtilityPresentation();\n            TickChestPresentation();'),
    ('pendingState != null && !recovering && !utilityDigPending ? pendingState : State',
     'pendingState != null && !recovering && !utilityDigPending && !ChestOpening ? pendingState : State'),
    ('pendingState != null && !recovering && View.GridStepComplete && utilitySeconds<=0',
     'pendingState != null && !recovering && !ChestOpening && View.GridStepComplete && utilitySeconds<=0'),
    ('        public void CancelPendingAction()\n        {\n            FinishUtilityPresentation();',
     '        public void CancelPendingAction()\n        {\n            FinishChestPresentation();\n            FinishUtilityPresentation();'),
    ('            GetTarget(out int x, out int z);\n            if (tool==0 && State.carryingProp==PropKind.None && State.carryingAnimal<0 && HabitatRules.IsRecoveryTarget(State,x,z))',
     '            GetTarget(out int x, out int z);\n'
     '            if (tool==0 && State.carryingProp==PropKind.None && State.carryingAnimal<0\n'
     '                && HabitatRules.TileAt(State,x,z)?.prop==PropKind.Chest\n'
     '                && !State.animals.Exists(a=>!a.carried&&a.x==x&&a.z==z))\n'
     '            { BeginChestAction(x,z); return; }\n'
     '            if (tool==0 && State.carryingProp==PropKind.None && State.carryingAnimal<0 && HabitatRules.IsRecoveryTarget(State,x,z))'),
]
old_chest = '''                case PropKind.Chest:
                    Box("Treasure box", p, new Vector3(0, .2f, 0), new Vector3(.64f, .38f, .48f), materials["wood"]);
                    Sphere("Rounded lid", p, new Vector3(0, .4f, 0), new Vector3(.65f, .30f, .48f), materials["woodLight"]);
                    for (int i = -1; i <= 1; i += 2) Box("Gold brace", p, new Vector3(i * .23f, .25f, -.25f), new Vector3(.065f, .46f, .03f), materials["yellow"]);
                    Box("Lock", p, new Vector3(0, .30f, -.27f), new Vector3(.12f, .14f, .05f), materials["yellow"]);
                    break;'''
new_chest = '''                case PropKind.Chest:
                    p.gameObject.AddComponent<HabitatChestVisual>().Build(materials["wood"],materials["woodLight"],materials["yellow"],materials["dark"]);
                    break;'''
view_edits = [
    ('        Transform BuildProp(Transform parent, PropKind kind, float y, int rotation)',
     '        public HabitatChestVisual ChestVisualAt(int x,int z)\n'
     '        {\n'
     '            if(state==null||tiles==null||!HabitatRules.Inside(state,x,z))return null;\n'
     '            Transform prop=tiles[x+z*state.width].prop;\n'
     '            return prop?prop.GetComponent<HabitatChestVisual>():null;\n'
     '        }\n\n'
     '        Transform BuildProp(Transform parent, PropKind kind, float y, int rotation)'),
    (old_chest, new_chest),
]

changes = []
for name, edits in [('HabitatGame.cs', game_edits), ('HabitatView.cs', view_edits)]:
    old = (scripts / name).read_text(encoding='utf-8-sig')
    new = old
    for before, after in edits:
        if after in new:
            continue
        if new.count(before) != 1:
            raise RuntimeError(f'{name}: expected exactly one unchanged anchor: {before[:100]!r}')
        new = new.replace(before, after, 1)
    changes.append((name, old, new))

review = stage / 'ReviewSources'
review.mkdir(exist_ok=True)
diff = []
for name, old, new in changes:
    (review / name).write_text(new, encoding='utf-8')
    diff.extend(difflib.unified_diff(old.splitlines(True), new.splitlines(True),
                                   fromfile='Assets/HabitatIsland/Scripts/' + name,
                                   tofile='Assets/HabitatIsland/Scripts/' + name))
(stage / 'ChestWiring.diff').write_text(''.join(diff), encoding='utf-8')

if args.apply:
    backup = project / 'ChangeBackups' / ('20261018_Chest_' + datetime.datetime.now().strftime('%H%M%S'))
    backup.mkdir(parents=True, exist_ok=False)
    for name, old, new in changes:
        shutil.copy2(scripts / name, backup / name)
        (scripts / name).write_text(new, encoding='utf-8')
    for name in ['HabitatGame.Chest.cs', 'HabitatChestVisual.cs']:
        if (scripts / name).exists():
            shutil.copy2(scripts / name, backup / name)
        shutil.copy2(stage / name, scripts / name)
    print('APPLIED precise chest wiring; backup:', backup)
else:
    print('STAGED ONLY; Assets untouched. Review:', review, 'Diff:', stage / 'ChestWiring.diff')
