using System;
using System.Collections.Generic;

namespace picovm.Packager.PE
{
    public sealed class PEImportLookupTable : List<KeyValuePair<PEImportLookupEntry, string?>>
    {
        private Dictionary<PEImportLookupEntry, string> queuedNameUpdates = new Dictionary<PEImportLookupEntry, string>();

        public void Add(PEImportLookupEntry entryWithoutName) => this.Add(new KeyValuePair<PEImportLookupEntry, string?>(entryWithoutName, null));
        public void QueueNameUpdate(PEImportLookupEntry entryWithoutName, string name)
        {
            if (queuedNameUpdates.ContainsKey(entryWithoutName))
            {
                if (!queuedNameUpdates[entryWithoutName].Equals(name))
                    throw new InvalidOperationException();
            }
            else
                queuedNameUpdates.Add(entryWithoutName, name);
        }
        public void ApplyNameUpdates()
        {
        again:
            foreach (var queued in queuedNameUpdates)
            {
                foreach (var entry in this)
                {
                    if (entry.Key.Equals(queued.Key) && entry.Value == null)
                    {
                        this.Remove(entry);
                        this.Add(new KeyValuePair<PEImportLookupEntry, string?>(entry.Key, queued.Value));
                        goto again;
                    }
                }
            }
        }
    }
}