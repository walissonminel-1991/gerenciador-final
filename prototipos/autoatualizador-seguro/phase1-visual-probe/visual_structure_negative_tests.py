"""Pure adversarial structural negative cases; never reads pixels or Registry."""
from visual_feasibility import structural_list_candidate, EMPTY_LABEL

BASE = [
    (11, EMPTY_LABEL, "WindowsForms10.STATIC.app", 99),
    (12, "", "WindowsForms10.Window.8.app", 99),
    (13, "", "WindowsForms10.Window.8.app", 12),
]
RECT = {11: (100,100,500,300), 12: (100,100,500,300),
        13: (485,100,500,300)}
def run(nodes, rects, expected):
    assert structural_list_candidate(nodes, rects) == expected

run(BASE, RECT, 12)
run(BASE[1:], RECT, None)  # hidden/missing empty label
run(BASE+[(14, "", "WindowsForms10.Window.8.app",99)],
    {**RECT, 14:(100,100,500,300)},None)  # two siblings
run([BASE[0],(12,"","WindowsForms10.Window.8.app",88),BASE[2]],RECT,None)
run([BASE[0],(12,"","WindowsForms10.EDIT.app",99),BASE[2]],RECT,None)
run(BASE,{**RECT,12:(100,100,510,310)},None)
run([BASE[0],(12,"SPOOF","WindowsForms10.Window.8.app",99),BASE[2]],
    RECT,None)
run([BASE[0],BASE[2]],RECT,None)  # unmatched sibling
run(BASE,{11:(100,100,500,300),13:(485,100,500,300)},None)
print("GF_PHASE1_LIST_STRUCTURE_MODEL " +
      '{"count":9,"invalid_rejected":8,"status":"passed",'
      '"scope":"MODEL_SNAPSHOTS_NOT_GENUINE_GF"}')
