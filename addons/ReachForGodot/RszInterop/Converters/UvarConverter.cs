namespace ReaGE;

using System.Diagnostics;
using System.Threading.Tasks;
using Godot;
using RszTool;

public class UvarConverter : ResourceConverter<UvarResource, UvarFile>
{
    public override UvarResource CreateOrReplaceResourcePlaceholder(AssetReference reference)
        => SetupResource(new UvarResource(), reference);

    public override UvarFile CreateFile(FileHandler fileHandler) => new UvarFile(fileHandler);

    public override async Task<bool> Import(UvarFile file, UvarResource target)
    {
        target.OriginalName = file.name;
        target.ResourceName = file.name;

        target.EmbeddedData = new UvarResource[file.embedCount];
        for (int i = 0; i < file.embedCount; ++i) {
            var embedded = target.EmbeddedData[i] ?? (target.EmbeddedData[i] = new UvarResource() { Game = Game });
            await Import(file.EmbeddedData[i], embedded);
        }

        target.Variables = new Godot.Collections.Array<UvarVariable>();
        for (var i = 0; i < file.Variables.Count; ++i) {
            var srcVar = file.Variables[i];
            var outVar = new UvarVariable() { Game = Game };
            outVar.Guid = srcVar.guid;
            outVar.ResourceName = srcVar.name;
            outVar.Flags = srcVar.flags;
            outVar.Value = UvarVariable.UvarVarToVariant(srcVar.value, srcVar.type, srcVar.flags);
            outVar.Type = srcVar.type;
            target.Variables.Add(outVar);
            if (srcVar.expression != null) {
                outVar.Expression = new UvarExpression();
                outVar.Expression.Connections = new Godot.Collections.Array<Vector4I>(srcVar.expression.Connections.Select(conn => new Vector4I(conn.nodeId, conn.inputSlot, conn.node2, conn.outputSlot)));
                outVar.Expression.Nodes = new UvarExpressionNode[srcVar.expression.nodeCount];
                for (int x = 0; x < srcVar.expression.nodeCount; ++x) {
                    var srcNode = srcVar.expression.Nodes[x];
                    var node = new UvarExpressionNode() {
                        NodeType = srcNode.name ?? string.Empty,
                        UnknownNumber = srcNode.uknCount,
                        ResourceName = srcNode.name,
                    };
                    outVar.Expression.Nodes[x] = node;
                    node.Parameters = new UvarExpressionNodeParameter[srcNode.parameters.Count];
                    for (var p = 0; p < srcNode.parameters.Count; p++) {
                        var srcParam = srcNode.parameters[p];
                        node.Parameters[p] = new UvarExpressionNodeParameter() {
                            SlotNameHash = srcParam.nameHash,
                            ValueType = srcParam.type,
                            Value = UvarExpressionNodeParameter.NodeVarToVariant(srcParam.value, srcParam.type),
                            ResourceName = $"[{srcParam.nameHash}] = {srcParam.value?.ToString() ?? "NULL"}",
                        };
                    }
                }

                outVar.Expression.Connections = new Godot.Collections.Array<Vector4I>();
                for (int x = 0; x < srcVar.expression.Connections.Count; ++x) {
                    var srcConn = srcVar.expression.Connections[x];
                    var conn = new Vector4I(srcConn.nodeId, srcConn.inputSlot, srcConn.node2, srcConn.outputSlot);
                    outVar.Expression.Connections.Add(conn);
                }
            }
        }

        return true;
    }

    public override async Task<bool> Export(UvarResource source, UvarFile file)
    {
        file.name = source.OriginalName;

        file.EmbeddedData.Clear();
        if (source.EmbeddedData != null) {
            foreach (var embed in source.EmbeddedData) {
                var subfile = new UvarFile(file.FileHandler.WithOffset(0));
                await Export(embed, subfile);
                file.EmbeddedData.Add(subfile);
            }
        }

        file.Variables.Clear();
        foreach (var srcVar in source.Variables!) {
            var outVar = new UvarFile.Variable();
            outVar.guid = srcVar.Guid;
            outVar.name = srcVar.ResourceName;
            outVar.flags = srcVar.Flags;
            outVar.value = UvarVariable.VariantToUvar(srcVar.Value, srcVar.Type, srcVar.Flags);
            outVar.type = srcVar.Type;
            file.Variables.Add(outVar);
            if (srcVar.Expression != null) {
                outVar.expression = new UvarFile.UvarExpression();
                int n = 0;
                foreach (var srcNode in srcVar.Expression.Nodes) {
                    var node = new UvarFile.UvarExpression.Node() {
                        name = srcNode.NodeType,
                        uknCount = srcNode.UnknownNumber,
                        nodeId = (short)n++,
                    };
                    outVar.expression.Nodes.Add(node);
                    if (srcNode.Parameters == null) continue;

                    foreach (var srcParam in srcNode.Parameters) {
                        node.parameters.Add(new UvarFile.UvarExpression.Node.NodeParameter() {
                            nameHash = srcParam.SlotNameHash,
                            type = srcParam.ValueType,
                            value = UvarExpressionNodeParameter.VariantToNodeVar(srcParam.Value, srcParam.ValueType),
                        });
                    }
                }

                foreach (var conn in srcVar.Expression.Connections!) {
                    outVar.expression.Connections.Add(new UvarFile.UvarExpression.NodeConnection() {
                        nodeId = (short)conn.X,
                        inputSlot = (short)conn.Y,
                        node2 = (short)conn.Z,
                        outputSlot = (short)conn.W
                    });
                }
            }
        }

        return true;
    }
}
