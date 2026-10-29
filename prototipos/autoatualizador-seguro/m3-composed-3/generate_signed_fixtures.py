"""Generate only pre-existing Stage11 public TEST signed fixtures in runner temp.
Requires precisely pinned python-tuf dependencies from Stage11; not production keys.
"""
import importlib.util
from pathlib import Path
import sys
base=Path(__file__).resolve().parent.parent
module_file=base/"stage11"/"make_fixtures.py"
spec=importlib.util.spec_from_file_location("gf_stage11_signed_fixture_generator",module_file)
assert spec and spec.loader
mod=importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)
target=Path(sys.argv[1]).resolve()
assert not target.exists() and not target.is_relative_to(base.parent)
mod.generate(target)
assert (target/"release2"/"targets.json").is_file()
assert (target/"release2"/"fixture-package.zip").is_file()
print("GF_L3_FIXTURE_SIGNED_PUBLIC_TEST_ONLY",flush=True)
