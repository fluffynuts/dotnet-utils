namespace asmdeps
{
    public class AssemblyDependencyInfo
    {
        public string FullName { get; set; }
        public bool Loaded { get; set; }
        public int Level { get; set; }

        public AssemblyDependencyInfo(string fullName, bool loaded, int level)
        {
            this.FullName = fullName;
            this.Loaded = loaded;
            this.Level = level;
        }
    }
}
