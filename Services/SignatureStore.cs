using System.Text.Json;

namespace KillerPDF.Services
{
    internal sealed class SignatureStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        private readonly string _dir;
        private readonly string _file;

        private static string DefaultDir => AppDataPaths.UserRoot;

        public SignatureStore()
            : this(DefaultDir, AppDataPaths.SignaturesFile) { }

        internal SignatureStore(string dir, string file)
        {
            _dir  = dir;
            _file = file;
        }

        private List<SavedSignature> _items = [];

        public IReadOnlyList<SavedSignature> Signatures => _items;

        public void Load()
        {
            try
            {
                if (System.IO.File.Exists(_file))
                {
                    var json = System.IO.File.ReadAllText(_file);
                    _items = JsonSerializer.Deserialize<List<SavedSignature>>(json) ?? [];
                }
            }
            catch { _items = []; }
        }

        public void Persist()
        {
            try
            {
                System.IO.Directory.CreateDirectory(_dir);
                var json = JsonSerializer.Serialize(_items, JsonOptions);
                System.IO.File.WriteAllText(_file, json);
            }
            catch { /* best effort */ }
        }

        public void Add(SavedSignature sig) => _items.Add(sig);

        public void Remove(SavedSignature sig) => _items.Remove(sig);
    }
}
