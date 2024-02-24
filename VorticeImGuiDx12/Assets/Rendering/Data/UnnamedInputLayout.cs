using System;
using System.Linq;

using Vortice.Direct3D12;

namespace Engine.Graphics;

public class UnnamedInputLayout : IEquatable<UnnamedInputLayout>
{
    public InputElementDescription[] inputElementDescriptions;

    public override bool Equals(object obj) =>
        Equals(obj as UnnamedInputLayout);

    public bool Equals(UnnamedInputLayout other)
    {
        if (ReferenceEquals(this, other)) 
            return true;

        return other is not null 
            && inputElementDescriptions.SequenceEqual(other.inputElementDescriptions);
    }

    public override int GetHashCode()
    {
        HashCode hashCode = new HashCode();
        for (int i = 0; i < inputElementDescriptions.Length; i++)
            hashCode.Add(inputElementDescriptions[i]);

        return hashCode.ToHashCode();
    }
}
