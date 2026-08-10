using System;
using System.Collections.Generic;

namespace picovm.Packager.PE
{
    public sealed class PEImportLookupTable : List<KeyValuePair<PEImportLookupEntry, string?>>
    {
        private Dictionary<PEImportLookupEntry, string> queuedNameUpdates = new Dictionary<PEImportLookupEntry, string>();

        public void Add(PEImportLookupEntry entryWithoutName) => Add(new KeyValuePair<PEImportLookupEntry, string?>(entryWithoutName, null));
        public void QueueNameUpdate(PEImportLookupEntry entryWithoutName, string name)
        {
            var added = queuedNameUpdates.TryAdd(entryWithoutName, name);
            if (!added)
            {
                var existing = queuedNameUpdates[entryWithoutName];
                if (!existing.Equals(name))
                    throw new InvalidOperationException($"Entry {entryWithoutName} already added to PE import lookup table with different name (existing={existing}, new={name})");
            }
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
                        Remove(entry);
                        Add(new KeyValuePair<PEImportLookupEntry, string?>(entry.Key, queued.Value));
                        goto again;
                    }
                }
            }
        }
    }
}