using System;
using System.Threading.Tasks;
using Godot;
using RszTool;

namespace ReaGE;

public interface IVisualREComponent
{
    Aabb GetBounds();
}
