"""Filesystem path helpers."""

import re
import shutil
from pathlib import Path

from pathvalidate import sanitize_filepath


def clean_setup_dir(path: Path | str, delete_content: bool = True) -> None:
    """Create or reset a directory at the given location.

    Args:
        path: Directory location to create.
        delete_content: When True (default), wipe any existing directory
            at ``path`` before recreating it. When False, leave existing
            contents in place and only ensure the directory exists.
    """
    p = Path(path)
    if p.is_dir() and delete_content:
        shutil.rmtree(p)
    p.mkdir(parents=True, exist_ok=not delete_content)


def sanitize_path(path: Path | str) -> Path:
    """Coerce an arbitrary string into a portable filesystem path.

    Thin wrapper around :func:`pathvalidate.sanitize_filepath` that also
    transliterates common German umlauts to their ASCII digraphs and
    collapses whitespace to underscores so the resulting path is safe to
    use on every platform.
    """
    cleaned = str(path)
    cleaned = re.sub("ü", "ue", cleaned)
    cleaned = re.sub("ä", "ae", cleaned)
    cleaned = re.sub("ö", "oe", cleaned)
    cleaned = sanitize_filepath(re.sub(r"\s", "_", cleaned), platform="universal")
    return Path(cleaned)
