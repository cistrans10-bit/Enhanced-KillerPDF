using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Objects;
using KillerPdf.Engine.Writing;

namespace KillerPdf.Engine.Signing;

/// <summary>Clears signature values that would become invalid after a document rewrite.</summary>
public static class PdfSignatureInvalidationWriter
{
    private static readonly PdfName AcroFormName = Name("AcroForm");
    private static readonly PdfName FieldsName = Name("Fields");
    private static readonly PdfName KidsName = Name("Kids");
    private static readonly PdfName FieldTypeName = Name("FT");
    private static readonly PdfName SignatureName = Name("Sig");
    private static readonly PdfName ValueName = Name("V");
    private static readonly PdfName PermissionsName = Name("Perms");

    /// <summary>
    /// Appends a revision that removes every signature field value and catalog certification
    /// permission while preserving the empty fields and their widgets for later re-signing.
    /// Returns the original bytes when the document contains no invalidatable signature state.
    /// </summary>
    public static byte[] ClearSignatureValues(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        PdfPageTree tree = PdfPageTree.Read(document);
        var update = new PdfIncrementalUpdateBuilder(document);
        bool changed = false;
        var catalogEntries = tree.Catalog.ToDictionary(entry => entry.Key, entry => entry.Value);
        if (catalogEntries.Remove(PermissionsName)) changed = true;

        if (tree.Catalog.TryGetValue(AcroFormName, out PdfObject? formValue))
        {
            Resolved formResolved = Resolve(document, formValue, "The catalog /AcroForm value");
            if (formResolved.Value is not PdfDictionary form)
                throw new InvalidOperationException("The catalog /AcroForm value is not a dictionary.");
            PdfDictionary rewrittenForm = form;
            if (form.TryGetValue(FieldsName, out PdfObject? fieldsValue))
            {
                Resolved fieldsResolved = Resolve(document, fieldsValue, "The AcroForm /Fields value");
                if (fieldsResolved.Value is not PdfArray fields)
                    throw new InvalidOperationException("The AcroForm /Fields value is not an array.");
                PdfArray rewrittenFields = RewriteArray(fields, null, 0);
                if (!ReferenceEquals(rewrittenFields, fields))
                {
                    changed = true;
                    PdfObject replacementFields = rewrittenFields;
                    if (fieldsResolved.Reference is PdfIndirectReference fieldsReference)
                    {
                        update.ReplaceObject(fieldsReference.ObjectNumber, rewrittenFields);
                        replacementFields = fieldsValue;
                    }
                    rewrittenForm = Replace(form, FieldsName, replacementFields);
                }
            }
            if (!ReferenceEquals(rewrittenForm, form))
            {
                if (formResolved.Reference is PdfIndirectReference formReference)
                    update.ReplaceObject(formReference.ObjectNumber, rewrittenForm);
                else
                    catalogEntries[AcroFormName] = rewrittenForm;
            }
        }

        if (!changed) return document.Source.ToArray();
        update.ReplaceObject(tree.CatalogReference.ObjectNumber,
            new PdfDictionary(catalogEntries));
        return update.Build();

        PdfArray RewriteArray(PdfArray fields, PdfName? inheritedType, int depth)
        {
            if (depth >= 256)
                throw new InvalidOperationException("The AcroForm field tree is too deeply nested.");
            bool arrayChanged = false;
            var values = new PdfObject[fields.Count];
            for (int index = 0; index < fields.Count; index++)
            {
                PdfObject original = fields[index];
                Resolved resolved = Resolve(document, original, "An AcroForm field");
                if (resolved.Value is not PdfDictionary field)
                    throw new InvalidOperationException("An AcroForm field is not a dictionary.");
                PdfName? type = inheritedType;
                if (field.TryGetValue(FieldTypeName, out PdfObject? typeValue))
                    type = Resolve(document, typeValue, "An AcroForm field /FT value").Value
                        as PdfName ?? throw new InvalidOperationException(
                            "An AcroForm field /FT value is not a name.");
                var entries = field.ToDictionary(entry => entry.Key, entry => entry.Value);
                bool fieldChanged = type?.Equals(SignatureName) == true
                    && entries.Remove(ValueName);
                if (field.TryGetValue(KidsName, out PdfObject? kidsValue))
                {
                    Resolved kidsResolved = Resolve(document, kidsValue, "An AcroForm field /Kids value");
                    if (kidsResolved.Value is not PdfArray kids)
                        throw new InvalidOperationException("An AcroForm field /Kids value is not an array.");
                    PdfArray rewrittenKids = RewriteArray(kids, type, depth + 1);
                    if (!ReferenceEquals(rewrittenKids, kids))
                    {
                        PdfObject replacementKids = rewrittenKids;
                        if (kidsResolved.Reference is PdfIndirectReference kidsReference)
                        {
                            update.ReplaceObject(kidsReference.ObjectNumber, rewrittenKids);
                            replacementKids = kidsValue;
                        }
                        entries[KidsName] = replacementKids;
                        fieldChanged = true;
                    }
                }
                if (!fieldChanged) { values[index] = original; continue; }
                changed = true;
                var rewritten = new PdfDictionary(entries);
                if (resolved.Reference is PdfIndirectReference reference)
                {
                    update.ReplaceObject(reference.ObjectNumber, rewritten);
                    values[index] = original;
                }
                else
                    values[index] = rewritten;
                arrayChanged = true;
            }
            return arrayChanged ? new PdfArray(values) : fields;
        }
    }

    private static PdfDictionary Replace(
        PdfDictionary source, PdfName name, PdfObject value) =>
        new(source.Where(entry => !entry.Key.Equals(name))
            .Append(new KeyValuePair<PdfName, PdfObject>(name, value)));

    private static Resolved Resolve(PdfDocument document, PdfObject value, string description)
    {
        PdfIndirectReference? reference = null;
        var visited = new HashSet<(int, int)>();
        for (int depth = 0; value is PdfIndirectReference current; depth++)
        {
            if (depth >= 64 || !visited.Add((current.ObjectNumber, current.Generation)))
                throw new InvalidOperationException($"{description} has an invalid reference chain.");
            reference = current;
            value = document.Resolve(current);
        }
        return new Resolved(value, reference);
    }

    private static PdfName Name(string value) => new(System.Text.Encoding.ASCII.GetBytes(value));
    private sealed record Resolved(PdfObject Value, PdfIndirectReference? Reference);
}
