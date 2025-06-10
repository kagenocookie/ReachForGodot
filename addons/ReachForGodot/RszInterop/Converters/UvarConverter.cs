namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using RszTool;

public class UvarConverter : ResourceConverter<UvarResource, UVarFile>
{
    public override UVarFile CreateFile(FileHandler fileHandler) => new UVarFile(fileHandler);

    public override async Task<bool> Import(UVarFile file, UvarResource target)
    {
        target.OriginalName = file.Header.name;
        target.ResourceName = file.Header.name;

        target.EmbeddedData = new UvarResource[file.Header.embedCount];
        for (int i = 0; i < file.Header.embedCount; ++i) {
            var embedded = target.EmbeddedData[i] ?? (target.EmbeddedData[i] = new UvarResource() { Game = Game });
            await Import(file.EmbeddedUVARs[i], embedded);
        }

        target.Variables = new Godot.Collections.Array<UvarVariable>();
        for (var i = 0; i < file.Variables.Count; ++i) {
            var srcVar = file.Variables[i];
            var outVar = new UvarVariable() { Game = Game };
            outVar.Guid = srcVar.guid;
            outVar.ResourceName = srcVar.Name;
            outVar.Flags = srcVar.flags;
            outVar.Value = UvarVariable.UvarVarToVariant(srcVar.Value, srcVar.type, srcVar.flags);
            outVar.Type = srcVar.type;
            target.Variables.Add(outVar);
            if (srcVar.Expression != null) {
                outVar.Expression = new UvarExpression();
                outVar.Expression.Connections = new Godot.Collections.Array<Vector4I>(srcVar.Expression.Connections.Select(conn => new Vector4I(conn.nodeId, conn.inputSlot, conn.node2, conn.outputSlot)));
                outVar.Expression.Nodes = new UvarExpressionNode[srcVar.Expression.nodeCount];
                outVar.Expression.OutputNodeId = srcVar.Expression.outputNodeId;
                outVar.Expression.UnknownId = srcVar.Expression.unknownCount;
                for (int x = 0; x < srcVar.Expression.nodeCount; ++x) {
                    var srcNode = srcVar.Expression.Nodes[x];
                    var node = new UvarExpressionNode() {
                        NodeType = srcNode.Name ?? string.Empty,
                        UnknownNumber = srcNode.uknCount,
                        ResourceName = srcNode.Name,
                    };
                    outVar.Expression.Nodes[x] = node;
                    node.Parameters = new UvarExpressionNodeParameter[srcNode.Parameters.Count];
                    for (var p = 0; p < srcNode.Parameters.Count; p++) {
                        var srcParam = srcNode.Parameters[p];
                        node.Parameters[p] = new UvarExpressionNodeParameter() {
                            SlotNameHash = srcParam.nameHash,
                            ValueType = srcParam.type,
                            Value = UvarExpressionNodeParameter.NodeVarToVariant(srcParam.value, srcParam.type),
                            ResourceName = $"[{srcParam.nameHash}] = {srcParam.value?.ToString() ?? "NULL"}",
                        };
                    }
                }

                outVar.Expression.Connections = new Godot.Collections.Array<Vector4I>();
                for (int x = 0; x < srcVar.Expression.Connections.Count; ++x) {
                    var srcConn = srcVar.Expression.Connections[x];
                    var conn = new Vector4I(srcConn.nodeId, srcConn.inputSlot, srcConn.node2, srcConn.outputSlot);
                    outVar.Expression.Connections.Add(conn);
                }
            }
        }

        return true;
    }

    public override async Task<bool> Export(UvarResource source, UVarFile file)
    {
        file.Header.name = source.OriginalName;

        file.EmbeddedUVARs.Clear();
        if (source.EmbeddedData != null) {
            foreach (var embed in source.EmbeddedData) {
                var subfile = new UVarFile(file.FileHandler.WithOffset(0));
                await Export(embed, subfile);
                file.EmbeddedUVARs.Add(subfile);
            }
        }

        file.Variables.Clear();
        foreach (var srcVar in source.Variables!) {
            var outVar = new RszTool.UVar.Variable();
            outVar.guid = srcVar.Guid;
            outVar.Name = srcVar.ResourceName;
            outVar.flags = srcVar.Flags;
            outVar.Value = UvarVariable.VariantToUvar(srcVar.Value, srcVar.Type, srcVar.Flags);
            outVar.type = srcVar.Type;
            file.Variables.Add(outVar);
            if (srcVar.Expression != null) {
                outVar.Expression = new RszTool.UVar.UvarExpression();
                int n = 0;
                outVar.Expression.outputNodeId = (short)srcVar.Expression.OutputNodeId;
                outVar.Expression.unknownCount = (short)srcVar.Expression.UnknownId;
                foreach (var srcNode in srcVar.Expression.Nodes) {
                    var node = new RszTool.UVar.UvarNode() {
                        Name = srcNode.NodeType,
                        uknCount = srcNode.UnknownNumber,
                        nodeId = (short)n++,
                    };
                    outVar.Expression.Nodes.Add(node);
                    if (srcNode.Parameters == null) continue;

                    foreach (var srcParam in srcNode.Parameters) {
                        node.Parameters.Add(new RszTool.UVar.NodeParameter() {
                            nameHash = srcParam.SlotNameHash,
                            type = srcParam.ValueType,
                            value = UvarExpressionNodeParameter.VariantToNodeVar(srcParam.Value, srcParam.ValueType),
                        });
                    }
                }

                foreach (var conn in srcVar.Expression.Connections!) {
                    outVar.Expression.Connections.Add(new RszTool.UVar.UvarExpression.NodeConnection() {
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
