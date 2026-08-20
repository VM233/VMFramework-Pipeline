#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace VMFramework.Pipeline.Editor
{
    internal sealed class VMFrameworkLocalizedStringReader
    {
        private readonly Dictionary<string, StringTableCollection> collections =
            new(StringComparer.Ordinal);

        internal VMFrameworkLocalizedStringSnapshot Read(LocalizedString reference,
            bool includeValues, ISet<string> locales, int maximumValues)
        {
            string tableName = GetTableName(reference);
            string key = GetKey(reference);
            var snapshot = new VMFrameworkLocalizedStringSnapshot
            {
                Table = tableName,
                Key = key,
            };
            if (!includeValues || string.IsNullOrWhiteSpace(tableName) ||
                string.IsNullOrWhiteSpace(key))
            {
                return snapshot;
            }

            StringTableCollection collection = GetCollection(tableName);
            if (collection == null)
            {
                snapshot.Values = new List<VMFrameworkLocalizedStringValue>();
                return snapshot;
            }

            List<StringTable> tables = collection.StringTables
                .Where(table => locales == null ||
                                locales.Contains(table.LocaleIdentifier.Code))
                .ToList();
            if (tables.Count > maximumValues)
            {
                throw new InvalidOperationException(
                    $"LocalizedString '{tableName}/{key}' matched {tables.Count} locales; " +
                    $"the request limit is {maximumValues}. Specify a narrower locales filter.");
            }

            snapshot.Values = tables.Select(table => new VMFrameworkLocalizedStringValue
            {
                Locale = table.LocaleIdentifier.Code,
                Value = table.GetEntry(key)?.Value ?? "",
            }).ToList();
            return snapshot;
        }

        internal string GetTableName(LocalizedString reference)
        {
            if (reference == null)
                return "";
            string name = reference.TableReference.TableCollectionName;
            return string.IsNullOrWhiteSpace(name)
                ? reference.TableReference.TableCollectionNameGuid.ToString()
                : name;
        }

        internal string GetKey(LocalizedString reference)
        {
            if (reference == null)
                return "";
            if (string.IsNullOrWhiteSpace(reference.TableEntryReference.Key) == false)
                return reference.TableEntryReference.Key;

            string tableName = GetTableName(reference);
            StringTableCollection collection = string.IsNullOrWhiteSpace(tableName)
                ? null
                : GetCollection(tableName);
            return collection?.SharedData.GetEntry(reference.TableEntryReference.KeyId)?.Key ?? "";
        }

        internal StringTableCollection GetCollection(string tableName)
        {
            if (!collections.TryGetValue(tableName, out StringTableCollection collection))
            {
                collection = LocalizationEditorSettings.GetStringTableCollection(tableName);
                collections.Add(tableName, collection);
            }
            return collection;
        }
    }
}
#endif
