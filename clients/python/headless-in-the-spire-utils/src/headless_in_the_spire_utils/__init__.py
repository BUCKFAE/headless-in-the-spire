"""headless-in-the-spire-utils — shared Python helpers.

Small, dependency-light utilities used by the wire client and agent
packages. Grow this surface lazily: every helper should either replace
copy-pasted code in two or more workspace members or sit behind a clear
"we'll need this soon" use case.
"""

from headless_in_the_spire_utils.paths import clean_setup_dir, sanitize_path

__version__ = "0.0.1"

__all__ = [
    "__version__",
    "clean_setup_dir",
    "sanitize_path",
]
