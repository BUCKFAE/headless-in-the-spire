namespace Sts2Headless.Utils;

// Setup-directory helpers, mirroring the Python `clean_setup_dir` in
// headless-in-the-spire-utils so the two toolchains share one notion of
// "create or reset this directory".
public static class SetupDir
{
    // Create the directory at `path`, optionally wiping any existing contents
    // first. With deleteContent=true (default) an existing directory is
    // removed and recreated empty; with deleteContent=false the directory is
    // ensured to exist but existing contents are left in place.
    public static void CleanSetupDir(string path, bool deleteContent = true)
    {
        if (Directory.Exists(path) && deleteContent)
        {
            Directory.Delete(path, recursive: true);
        }
        Directory.CreateDirectory(path);
    }
}
