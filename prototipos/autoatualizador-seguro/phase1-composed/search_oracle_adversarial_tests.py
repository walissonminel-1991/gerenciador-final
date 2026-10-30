"""A1/A2 algorithm counterexamples. Executes on disposable Windows CI only.

These are modeled Win32 control snapshots, NOT full GF/Windows UI evidence.
"""
import json
from win32_process_observation import search_snapshot_proves

NAME = "GF-PHASE1-INVENTORY-SIMULATED"
EMPTY = "Nenhum aplicativo corresponde à pesquisa."
EDIT, PARENT = 10, 25

def snapshot(*, empty_exists=True, empty_visible=False, item_exists=False,
             item_visible=True, item_parent=100, wrong_item=False):
    rows = [(EDIT, NAME, "EDIT", PARENT, True)]
    if empty_exists:
        rows.append((15, EMPTY, "STATIC", 100, empty_visible))
    if item_exists:
        rows.append((33, NAME + ("-WRONG" if wrong_item else ""),
                     "STATIC", item_parent, item_visible))
    return rows

def run_case(name, rows, query, want_empty, expected):
    outcome = search_snapshot_proves(rows, query, query, want_empty,
                                    EDIT, PARENT, EMPTY)
    if outcome is not expected:
        raise AssertionError(name + ": false classification " + str(outcome))
    return name

cases = []
cases.append(run_case("A1_absent_item_broken_empty_warning",
                      snapshot(empty_exists=False), NAME, False, False))
cases.append(run_case("A1_absent_item_hidden_empty_warning",
                      snapshot(), NAME, False, False))
cases.append(run_case("A1_stale_wrong_item", snapshot(item_exists=True, wrong_item=True),
                      NAME, False, False))
cases.append(run_case("A1_actual_visible_item",
                      snapshot(item_exists=True), NAME, False, True))
cases.append(run_case("A1_missing_item_control",
                      snapshot(item_exists=True, item_visible=False),
                      NAME, False, False))
cases.append(run_case("A1_search_control_echo_not_a_row",
                      snapshot(item_exists=True, item_parent=PARENT),
                      NAME, False, False))
cases.append(run_case("A2_no_seed_negative_control_visible",
                      snapshot(empty_visible=True), NAME, True, True))
cases.append(run_case("A2_no_seed_empty_control_missing",
                      snapshot(empty_exists=False), NAME, True, False))
cases.append(run_case("A2_negative_control_contains_item",
                      snapshot(empty_visible=True, item_exists=True),
                      NAME, True, False))
cases.append(run_case("A2_negative_control_hidden",
                      snapshot(empty_visible=False), NAME, True, False))
print("GF_PHASE1_SEARCH_ORACLE_ADVERSARIAL " +
      json.dumps({"status": "passed", "scope": "MODELED_WIN32_SNAPSHOTS_NOT_GF",
                  "cases": cases, "count": len(cases)},
                 sort_keys=True))
