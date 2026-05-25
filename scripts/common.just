# Shared settings for every module justfile. Imported via
# `import '<relative-path>/common.just'` from each module so variables
# live in one place. `set` and variable definitions in an imported file
# affect the importing file's scope (unlike `mod`, which namespaces).

set dotenv-load

# MSBuild parallelism. "auto" lets MSBuild pick (`-maxcpucount`); override
# with `BUILD_CORES=4 just …` to cap.
BUILD_CORES := "auto"
MSBUILD_MAX_CPU := if BUILD_CORES == "auto" { "-maxcpucount" } else { "-maxcpucount:" + BUILD_CORES }

# xUnit max parallel test threads for the per-suite recipes
# (validation::dotnet::test-unit / test-integration / test-end2end).
# Local default is 0 ("# of CPU cores"); CI should export
# XUNIT_THREADS=2 (or similar) to cap host-subprocess fan-out.
#
# `just validation::test` fans out all suites concurrently and uses its
# own per-suite caps (XUNIT_THREADS_INTEGRATION / _END2END / _UNIT) —
# see scripts/validation/test-parallel.sh.
XUNIT_THREADS := env_var_or_default("XUNIT_THREADS", "0")
