using System;
using System.Threading.Tasks;
using Godot;
using RszTool;

namespace RGE;

public interface IVisualREComponent
{
    Aabb GetBounds();
}
