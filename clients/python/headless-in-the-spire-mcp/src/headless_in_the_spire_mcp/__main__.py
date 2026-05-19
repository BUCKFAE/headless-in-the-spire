"""Entry point so `python -m headless_in_the_spire_mcp` launches the server.

The `headless-in-the-spire-mcp` console script (declared in pyproject.toml)
also resolves to this same `main`, so both invocations are equivalent.
"""

from headless_in_the_spire_mcp.server import main

if __name__ == "__main__":
    main()
