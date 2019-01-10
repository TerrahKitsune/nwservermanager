using nwservermanager.Manager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nwservermanager
{
    public class JsonFileValidator : IServerValidator
    {
        private readonly string _file;
        private readonly IFeedback _feedback;

        private Dictionary<Guid, string> _clients;
        private DateTime _lastwrite;

        public JsonFileValidator(string filename, IFeedback feedback)
        {
            _file = Path.GetFullPath(filename);
            _feedback = feedback;

            if (!File.Exists(_file))
            {
                Dictionary<Guid, string> data = new Dictionary<Guid, string>
                {
                    { Guid.Parse("14f3a874-224b-49fc-9437-37452926d37e"), "Test console" },
                    { Guid.Parse("14f3a874-224b-49fc-9437-37452926d37f"), "Test nwserver" }
                };

                string raw = Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(_file, raw);
            }
        }

        private void GetClients()
        {
            try
            {
                if (_clients == null || File.GetLastWriteTime(_file) != _lastwrite)
                {
                    string raw = File.ReadAllText(_file);

                    _clients = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<Guid, string>>(raw);

                    _lastwrite = File.GetLastWriteTime(_file);
                }
            }
            catch(Exception ex)
            {
                _feedback.Error(ex, true);
                _clients = _clients ?? new Dictionary<Guid, string>();
            }
        }

        public string GetClientName(Guid id)
        {
            if (id == Guid.Empty)
            {
                return "Invalid";
            }

            lock (this)
            {
                GetClients();
                if (_clients.TryGetValue(id, out string value))
                {
                    return value;
                }
                else
                {
                    return "Unknown";
                }
            }
        }

        public bool ServerIsAllowed(Guid id)
        {
            if(id == Guid.Empty)
            {
                return false;
            }

            lock (this)
            {
                GetClients();
                return _clients.ContainsKey(id);
            }
        }
    }
}
