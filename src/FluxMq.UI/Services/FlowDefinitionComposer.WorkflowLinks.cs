using System.Text.Json.Nodes;

namespace FluxMq.UI.Services;

public sealed partial class FlowDefinitionComposer
{
    private static string BuildPortReference(string sourceNodeName, string sourcePortName)
    {
        var sourcePort = string.IsNullOrWhiteSpace(sourcePortName) ? "Output" : sourcePortName.Trim();
        return $"{sourceNodeName.Trim()}.{sourcePort}";
    }

    private static void AppendLinkReference(JsonObject targetNode, string targetPortName, string reference)
    {
        if (targetNode[targetPortName] is not { } existing)
        {
            targetNode[targetPortName] = reference;
            return;
        }

        if (ContainsLinkReference(existing, reference))
        {
            return;
        }

        if (existing is JsonArray existingArray)
        {
            existingArray.Add(JsonValue.Create(reference));
            return;
        }

        targetNode[targetPortName] = new JsonArray(existing.DeepClone(), JsonValue.Create(reference));
    }

    private static bool RemoveLinkReference(
        JsonObject targetNode,
        string targetPortName,
        string sourceNodeName,
        string sourcePortName)
    {
        if (targetNode[targetPortName] is not { } existing)
        {
            return false;
        }

        var updated = RemoveLinkReference(existing, sourceNodeName, sourcePortName, out var removed);
        if (!removed)
        {
            return false;
        }

        if (updated is null)
        {
            targetNode.Remove(targetPortName);
        }
        else
        {
            targetNode[targetPortName] = updated;
        }

        return true;
    }

    private static bool TryGetLinkCondition(
        JsonNode node,
        string sourceNodeName,
        string sourcePortName,
        out string? condition)
    {
        condition = null;

        if (node is JsonValue value &&
            value.TryGetValue<string>(out var reference) &&
            ReferenceMatches(reference, sourceNodeName, sourcePortName))
        {
            return true;
        }

        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not null && TryGetLinkCondition(item, sourceNodeName, sourcePortName, out condition))
                {
                    return true;
                }
            }

            return false;
        }

        if (node is JsonObject obj &&
            (obj.TryGetPropertyValue("from", out var fromNode) ||
             obj.TryGetPropertyValue("From", out fromNode)) &&
            fromNode is not null &&
            ContainsLinkReference(fromNode, BuildPortReference(sourceNodeName, sourcePortName)))
        {
            if ((obj.TryGetPropertyValue("when", out var whenNode) ||
                 obj.TryGetPropertyValue("When", out whenNode)) &&
                whenNode is JsonValue whenValue &&
                whenValue.TryGetValue<string>(out var conditionValue))
            {
                condition = conditionValue;
            }

            return true;
        }

        return false;
    }

    private static bool UpdateLinkCondition(
        JsonObject targetNode,
        string targetPortName,
        string sourceNodeName,
        string sourcePortName,
        string? condition)
    {
        if (targetNode[targetPortName] is not { } existing)
        {
            return false;
        }

        var updated = UpdateLinkCondition(existing, sourceNodeName, sourcePortName, condition, out var changed);
        if (!changed)
        {
            return false;
        }

        targetNode[targetPortName] = updated;
        return true;
    }

    private static JsonNode UpdateLinkCondition(
        JsonNode node,
        string sourceNodeName,
        string sourcePortName,
        string? condition,
        out bool changed)
    {
        changed = false;
        var normalizedCondition = string.IsNullOrWhiteSpace(condition) ? null : condition.Trim();

        if (node is JsonValue value &&
            value.TryGetValue<string>(out var reference) &&
            ReferenceMatches(reference, sourceNodeName, sourcePortName))
        {
            changed = true;
            return normalizedCondition is null
                ? JsonValue.Create(reference)!
                : new JsonObject
                {
                    ["from"] = reference,
                    ["when"] = normalizedCondition
                };
        }

        if (node is JsonArray array)
        {
            var updatedArray = new JsonArray();
            foreach (var item in array)
            {
                if (item is null)
                {
                    updatedArray.Add(null);
                    continue;
                }

                var updatedItem = UpdateLinkCondition(item, sourceNodeName, sourcePortName, normalizedCondition, out var itemChanged);
                changed |= itemChanged;
                updatedArray.Add(updatedItem);
            }

            return changed ? updatedArray : node.DeepClone();
        }

        if (node is JsonObject obj &&
            (obj.TryGetPropertyValue("from", out var fromNode) ||
             obj.TryGetPropertyValue("From", out fromNode)) &&
            fromNode is not null &&
            ContainsLinkReference(fromNode, BuildPortReference(sourceNodeName, sourcePortName)))
        {
            changed = true;
            var updatedObject = (JsonObject)node.DeepClone();
            updatedObject["from"] = fromNode.DeepClone();
            updatedObject.Remove("From");
            updatedObject.Remove("When");

            if (normalizedCondition is null)
            {
                updatedObject.Remove("when");
                if (updatedObject.Count == 1 &&
                    updatedObject.TryGetPropertyValue("from", out var normalizedFrom) &&
                    normalizedFrom is JsonValue normalizedValue &&
                    normalizedValue.TryGetValue<string>(out var normalizedReference))
                {
                    return JsonValue.Create(normalizedReference)!;
                }
            }
            else
            {
                updatedObject["when"] = normalizedCondition;
            }

            return updatedObject;
        }

        return node.DeepClone();
    }

    private static JsonNode? RemoveLinkReference(
        JsonNode node,
        string sourceNodeName,
        string sourcePortName,
        out bool removed)
    {
        removed = false;

        if (node is JsonValue value &&
            value.TryGetValue<string>(out var reference) &&
            ReferenceMatches(reference, sourceNodeName, sourcePortName))
        {
            removed = true;
            return null;
        }

        if (node is JsonArray array)
        {
            var updatedArray = new JsonArray();
            foreach (var item in array)
            {
                if (item is null)
                {
                    updatedArray.Add(null);
                    continue;
                }

                var updatedItem = RemoveLinkReference(item, sourceNodeName, sourcePortName, out var itemRemoved);
                removed |= itemRemoved;
                if (updatedItem is not null)
                {
                    updatedArray.Add(updatedItem);
                }
                else if (!itemRemoved)
                {
                    updatedArray.Add(item.DeepClone());
                }
            }

            return removed
                ? updatedArray.Count == 0 ? null : updatedArray
                : node.DeepClone();
        }

        if (node is JsonObject obj &&
            (obj.TryGetPropertyValue("from", out var fromNode) ||
             obj.TryGetPropertyValue("From", out fromNode)) &&
            fromNode is not null)
        {
            var updatedFrom = RemoveLinkReference(fromNode, sourceNodeName, sourcePortName, out removed);
            if (!removed)
            {
                return node.DeepClone();
            }

            if (updatedFrom is null)
            {
                return null;
            }

            var updatedObject = (JsonObject)node.DeepClone();
            updatedObject["from"] = updatedFrom;
            updatedObject.Remove("From");
            return updatedObject;
        }

        return node.DeepClone();
    }

    private static JsonNode? RemoveReferencesFromSourceNode(
        JsonNode node,
        string sourceNodeName,
        out bool removed)
    {
        removed = false;

        if (node is JsonValue value &&
            value.TryGetValue<string>(out var reference) &&
            ReferenceNodeMatches(reference, sourceNodeName))
        {
            removed = true;
            return null;
        }

        if (node is JsonArray array)
        {
            var updatedArray = new JsonArray();
            foreach (var item in array)
            {
                if (item is null)
                {
                    updatedArray.Add(null);
                    continue;
                }

                var updatedItem = RemoveReferencesFromSourceNode(item, sourceNodeName, out var itemRemoved);
                removed |= itemRemoved;
                if (updatedItem is not null)
                {
                    updatedArray.Add(updatedItem);
                }
                else if (!itemRemoved)
                {
                    updatedArray.Add(item.DeepClone());
                }
            }

            return removed
                ? updatedArray.Count == 0 ? null : updatedArray
                : node.DeepClone();
        }

        if (node is JsonObject obj &&
            (obj.TryGetPropertyValue("from", out var fromNode) ||
             obj.TryGetPropertyValue("From", out fromNode)) &&
            fromNode is not null)
        {
            var updatedFrom = RemoveReferencesFromSourceNode(fromNode, sourceNodeName, out removed);
            if (!removed)
            {
                return node.DeepClone();
            }

            if (updatedFrom is null)
            {
                return null;
            }

            var updatedObject = (JsonObject)node.DeepClone();
            updatedObject["from"] = updatedFrom;
            updatedObject.Remove("From");
            return updatedObject;
        }

        return node.DeepClone();
    }

    private static JsonNode RenameReferencesFromSourceNode(
        JsonNode node,
        string sourceNodeName,
        string newSourceNodeName,
        out bool renamed)
    {
        renamed = false;

        if (node is JsonValue value &&
            value.TryGetValue<string>(out var reference) &&
            ReferenceNodeMatches(reference, sourceNodeName))
        {
            renamed = true;
            return JsonValue.Create(RenameReferenceNode(reference, sourceNodeName, newSourceNodeName))!;
        }

        if (node is JsonArray array)
        {
            var updatedArray = new JsonArray();
            foreach (var item in array)
            {
                if (item is null)
                {
                    updatedArray.Add(null);
                    continue;
                }

                var updatedItem = RenameReferencesFromSourceNode(item, sourceNodeName, newSourceNodeName, out var itemRenamed);
                renamed |= itemRenamed;
                updatedArray.Add(updatedItem);
            }

            return renamed ? updatedArray : node.DeepClone();
        }

        if (node is JsonObject obj &&
            (obj.TryGetPropertyValue("from", out var fromNode) ||
             obj.TryGetPropertyValue("From", out fromNode)) &&
            fromNode is not null)
        {
            var updatedFrom = RenameReferencesFromSourceNode(fromNode, sourceNodeName, newSourceNodeName, out renamed);
            if (!renamed)
            {
                return node.DeepClone();
            }

            var updatedObject = (JsonObject)node.DeepClone();
            updatedObject["from"] = updatedFrom;
            updatedObject.Remove("From");
            return updatedObject;
        }

        return node.DeepClone();
    }

    private static bool ContainsLinkReference(JsonNode node, string reference)
    {
        if (node is JsonValue value &&
            value.TryGetValue<string>(out var existingReference) &&
            string.Equals(existingReference, reference, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (node is JsonArray array)
        {
            return array.Any(item => item is not null && ContainsLinkReference(item, reference));
        }

        if (node is JsonObject obj &&
            (obj.TryGetPropertyValue("from", out var fromNode) ||
             obj.TryGetPropertyValue("From", out fromNode)) &&
            fromNode is not null)
        {
            return ContainsLinkReference(fromNode, reference);
        }

        return false;
    }

    private static bool ReferenceMatches(string reference, string sourceNodeName, string sourcePortName)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return false;
        }

        var parts = reference.Trim().Split('.', 2, StringSplitOptions.TrimEntries);
        var referenceNode = parts[0];
        var referencePort = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])
            ? parts[1]
            : "Output";
        var sourcePort = string.IsNullOrWhiteSpace(sourcePortName) ? "Output" : sourcePortName.Trim();

        return string.Equals(referenceNode, sourceNodeName.Trim(), StringComparison.Ordinal) &&
               string.Equals(referencePort, sourcePort, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ReferenceNodeMatches(string reference, string sourceNodeName)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return false;
        }

        var referenceNode = reference.Trim().Split('.', 2, StringSplitOptions.TrimEntries)[0];
        return string.Equals(referenceNode, sourceNodeName.Trim(), StringComparison.Ordinal);
    }

    private static string RenameReferenceNode(string reference, string sourceNodeName, string newSourceNodeName)
    {
        var parts = reference.Trim().Split('.', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 0 ||
            !string.Equals(parts[0], sourceNodeName.Trim(), StringComparison.Ordinal))
        {
            return reference;
        }

        return parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])
            ? $"{newSourceNodeName.Trim()}.{parts[1]}"
            : newSourceNodeName.Trim();
    }
}
