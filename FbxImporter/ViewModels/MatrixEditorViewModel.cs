using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using FbxImporter.Util;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace FbxImporter.ViewModels;

// One editable cell of the 3x3 grid. The text is parsed with the invariant culture (a comma is also
// accepted as the decimal separator) so input does not depend on the OS regional settings.
public class MatrixCellViewModel : ViewModelBase
{
    public MatrixCellViewModel(float value)
    {
        Text = Format(value);
        this.WhenAnyValue(x => x.Text).Select(x => !TryParse(x, out _)).ToPropertyEx(this, x => x.IsInvalid);
    }

    [Reactive] public string Text { get; set; } = string.Empty;

    [ObservableAsProperty] public bool IsInvalid { get; set; }

    public static string Format(float value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    public static bool TryParse(string? text, out float value)
    {
        value = 0f;
        if (string.IsNullOrWhiteSpace(text)) return false;
        return float.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture,
            out value) && float.IsFinite(value);
    }
}

// Editor for a single 3x3 matrix: nine text cells plus a preset picker.
// Value is null while any cell is not a valid number (or the matrix is not invertible, if required).
public class MatrixEditorViewModel : ViewModelBase
{
    private readonly bool _requireInvertible;
    private bool _updating;

    public MatrixEditorViewModel(string title, Matrix3x3 initial, IReadOnlyList<Matrix3x3Preset> presets,
        bool requireInvertible)
    {
        Title = title;
        Presets = presets;
        _requireInvertible = requireInvertible;
        Cells = initial.ToRowMajor().Select(x => new MatrixCellViewModel(x)).ToList();

        Recompute();
        SelectedPreset = Value is { } v ? presets.FirstOrDefault(x => x.Matrix == v) : null;

        Observable.Merge(Cells.Select(c => c.WhenAnyValue(x => x.Text).Skip(1)))
            .Subscribe(_ => OnCellEdited());
        this.WhenAnyValue(x => x.SelectedPreset).Skip(1).Where(x => x is not null)
            .Subscribe(x => ApplyPreset(x!));
    }

    public string Title { get; }

    public IReadOnlyList<Matrix3x3Preset> Presets { get; }

    // Row-major: M11, M12, M13, M21, ...
    public IReadOnlyList<MatrixCellViewModel> Cells { get; }

    [Reactive] public Matrix3x3Preset? SelectedPreset { get; set; }

    [Reactive] public Matrix3x3? Value { get; set; }

    [Reactive] public string? Error { get; set; }

    private void OnCellEdited()
    {
        if (_updating) return;
        Recompute();

        // Show the matching preset, or "Custom" (null) when the cells match none of them.
        _updating = true;
        try
        {
            SelectedPreset = Value is { } v ? Presets.FirstOrDefault(x => x.Matrix == v) : null;
        }
        finally
        {
            _updating = false;
        }
    }

    private void ApplyPreset(Matrix3x3Preset preset)
    {
        if (_updating) return;
        _updating = true;
        try
        {
            float[] values = preset.Matrix.ToRowMajor();
            for (int i = 0; i < Cells.Count; i++)
            {
                Cells[i].Text = MatrixCellViewModel.Format(values[i]);
            }
        }
        finally
        {
            _updating = false;
        }

        Recompute();
    }

    private void Recompute()
    {
        float[] values = new float[Matrix3x3.ElementCount];
        for (int i = 0; i < values.Length; i++)
        {
            if (MatrixCellViewModel.TryParse(Cells[i].Text, out values[i])) continue;
            Value = null;
            Error = "Every cell needs a valid number.";
            return;
        }

        Matrix3x3 matrix = Matrix3x3.FromRowMajor(values);
        if (_requireInvertible && matrix.IsSingular)
        {
            Value = null;
            Error = "Matrix is singular (determinant is 0), it cannot be inverted.";
            return;
        }

        Value = matrix;
        Error = null;
    }
}
