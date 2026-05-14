"""Verify the workspace dependency on headless-in-the-spire resolves.

This is the only thing worth testing while the package is empty: that
`uv sync --all-packages` correctly installs the sibling wire client as
an editable dep, so future agents can `from headless_in_the_spire import
Client` without any extra setup. If this breaks, the workspace wiring
in pyproject.toml regressed.
"""

import headless_in_the_spire_agents


def test_package_imports() -> None:
    assert headless_in_the_spire_agents.__version__


def test_client_dependency_resolves() -> None:
    from headless_in_the_spire import Client

    # The import resolving is the assertion: pyright would catch a typing
    # break and runtime would catch a missing dep. Touching `Client` here
    # so neither tool flags it unused.
    assert Client.__name__ == "Client"
