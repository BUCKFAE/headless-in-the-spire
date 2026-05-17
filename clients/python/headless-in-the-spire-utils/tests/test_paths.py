"""Tests for headless_in_the_spire_utils.paths."""

from pathlib import Path

import pytest
from headless_in_the_spire_utils.paths import clean_setup_dir, sanitize_path

# ── clean_setup_dir ─────────────────────────────────────────────────────


def test_clean_setup_dir_creates_missing_directory(tmp_path: Path):
    target = tmp_path / "fresh"
    clean_setup_dir(target)
    assert target.is_dir()
    assert list(target.iterdir()) == []


def test_clean_setup_dir_creates_parents(tmp_path: Path):
    target = tmp_path / "a" / "b" / "c"
    clean_setup_dir(target)
    assert target.is_dir()


def test_clean_setup_dir_accepts_str_path(tmp_path: Path):
    target = tmp_path / "from-str"
    clean_setup_dir(str(target))
    assert target.is_dir()


def test_clean_setup_dir_wipes_existing_content_by_default(tmp_path: Path):
    target = tmp_path / "dirty"
    target.mkdir()
    (target / "stale.txt").write_text("old")
    (target / "sub").mkdir()
    (target / "sub" / "more.txt").write_text("nested")

    clean_setup_dir(target)

    assert target.is_dir()
    assert list(target.iterdir()) == []


def test_clean_setup_dir_preserves_content_when_delete_content_false(tmp_path: Path):
    target = tmp_path / "keep"
    target.mkdir()
    (target / "leave-me.txt").write_text("hello")

    clean_setup_dir(target, delete_content=False)

    assert target.is_dir()
    assert (target / "leave-me.txt").read_text() == "hello"


def test_clean_setup_dir_delete_content_false_is_idempotent_on_existing_dir(tmp_path: Path):
    # exist_ok=True path: calling twice must not raise.
    target = tmp_path / "twice"
    clean_setup_dir(target, delete_content=False)
    clean_setup_dir(target, delete_content=False)
    assert target.is_dir()


# ── sanitize_path ───────────────────────────────────────────────────────


def test_sanitize_path_returns_path_instance(tmp_path: Path):
    result = sanitize_path("plain")
    assert isinstance(result, Path)
    assert str(result) == "plain"


def test_sanitize_path_transliterates_lowercase_umlauts():
    # Only lowercase ü/ä/ö are transliterated today; ß and uppercase
    # variants are out of scope (and pass through untouched).
    result = sanitize_path("grüße/bär/öl")
    assert str(result) == "grueße/baer/oel"


def test_sanitize_path_collapses_whitespace_to_underscore():
    result = sanitize_path("hello world\tindeed\nyes")
    assert str(result) == "hello_world_indeed_yes"


def test_sanitize_path_strips_characters_invalid_across_platforms():
    # The "universal" platform rejects characters that any major OS
    # disallows (e.g. ':', '*', '?', '<', '>', '|', '"').
    result = sanitize_path("naughty:name*?.txt")
    s = str(result)
    for ch in ':*?<>|"':
        assert ch not in s


def test_sanitize_path_accepts_path_input(tmp_path: Path):
    # Constructing a Path from "ä b" yields a single path segment on
    # POSIX; the sanitizer should still rewrite the umlaut and the space.
    result = sanitize_path(Path("ä b"))
    assert str(result) == "ae_b"


@pytest.mark.parametrize(
    ("raw", "expected"),
    [
        ("über/cool", "ueber/cool"),
        ("schöne neue welt", "schoene_neue_welt"),
        ("nothing-to-do", "nothing-to-do"),
        # ß is intentionally not transliterated by this helper.
        ("straße", "straße"),
        # Uppercase umlauts also pass through unchanged (lowercase only).
        ("Über", "Über"),
    ],
)
def test_sanitize_path_parametrized(raw: str, expected: str):
    assert str(sanitize_path(raw)) == expected
