const
    os = require("os"),
    { ExecStepContext } = require("exec-step"),
    { folderExists, copyFile, ls, FsEntities, CopyFileOptions } = require("yafs"),
    path = require("path"),
    gulp = requireModule("gulp");

gulp.task("copy-local", async () => {
    const envVar = os.platform() === "win32"
        ? "USERPROFILE"
        : "HOME";
    const
        homeFolder = process.env[envVar],
        target = path.join(homeFolder, ".local", "bin"),
        targetExists = await folderExists(target);
    if (!targetExists) {
        return;
    }
    const
        ctx = new ExecStepContext(),
        sources = await ls("bin", { entities: FsEntities.files, fullPaths: true });
    for (const source of sources) {
        const
            fileName = path.basename(source),
            targetFile = path.join(target, fileName);

        await ctx.exec(
            `copy local: ${fileName}`,
            async () => await copyFile(source, targetFile, CopyFileOptions.overwriteExisting)
        );
    }
});