using System.Numerics;
using ReactiveUI.Fody.Helpers;

namespace FbxImporter.ViewModels;

// ReSharper disable once InconsistentNaming
public class Matrix3x3ViewModel : ViewModelBase
{
    // Identity constructor
    public Matrix3x3ViewModel()
    {
        M11 = 1; M12 = 0; M13 = 0;
        M21 = 0; M22 = 1; M23 = 0;
        M31 = 0; M32 = 0; M33 = 1;
    }

    public Matrix3x3ViewModel(Matrix4x4 m)
    {
        M11 = m.M11; M12 = m.M12; M13 = m.M13;
        M21 = m.M21; M22 = m.M22; M23 = m.M23;
        M31 = m.M31; M32 = m.M32; M33 = m.M33;
    }
    
    [Reactive] public float M11 { get; set; }
    [Reactive] public float M12 { get; set; }
    [Reactive] public float M13 { get; set; }
    [Reactive] public float M21 { get; set; }
    [Reactive] public float M22 { get; set; }
    [Reactive] public float M23 { get; set; }
    [Reactive] public float M31 { get; set; }
    [Reactive] public float M32 { get; set; }
    [Reactive] public float M33 { get; set; }

    public Matrix4x4 ToMatrix()
    {
        return Matrix4x4.Identity with
        {
            M11 = M11, M12 = M12, M13 = M13,
            M21 = M21, M22 = M22, M23 = M23,
            M31 = M31, M32 = M32, M33 = M33,
        };
    }
}