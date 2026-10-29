"""Prepare the EXISTING Stage8 pinned-source fixture outside the repository.
No patch edits, no product dependencies, no Python oracle and no old test rerun.
"""
import importlib.util
import pathlib
import sys

here = pathlib.Path(__file__).resolve().parent
entry = here.parent / "stage8" / "prepare_source.py"
spec = importlib.util.spec_from_file_location("gf_composed_pinned_stage8", entry)
assert spec is not None and spec.loader is not None
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
root = pathlib.Path(sys.argv[1]).resolve()
root.mkdir(parents=True, exist_ok=True)
assert not root.is_relative_to(here.parent.parent.parent)
source, provenance = module.prepare(root)
assert provenance["original_nuget_package_changed"] is False
assert provenance["crypto_parser_sources_unchanged"] is True
print(source, flush=True)
