using System;
using System.Linq;

using Vortice.Direct3D12;

namespace Engine.DataTypes;

public class UnnamedInputLayout : IEquatable<UnnamedInputLayout>
{
    public InputElementDescription[] InputElementDescriptions;

    public override bool Equals(object obj) =>
        Equals(obj as UnnamedInputLayout);

    public bool Equals(UnnamedInputLayout other)
    {
        if (ReferenceEquals(this, other)) 
            return true;

        return other is not null 
            && InputElementDescriptions.SequenceEqual(other.InputElementDescriptions);
    }

    public override int GetHashCode()
    {
        HashCode hashCode = new HashCode();
        for (int i = 0; i < InputElementDescriptions.Length; i++)
            hashCode.Add(InputElementDescriptions[i]);

        return hashCode.ToHashCode();
    }
}
