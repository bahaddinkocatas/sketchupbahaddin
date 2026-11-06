using Sketch.Geometry;

namespace Sketch.Model.Tests;

public class MatrixTests
{
    [Fact]
    public void Inverse_times_matrix_is_identity()
    {
        var m = Matrix4d.Translation(new Vector3d(3, -4, 5)) * Matrix4d.Rotation(new Vector3d(1, 2, 3), 0.8)
                * Matrix4d.Scaling(2, 3, 0.5);
        var id = m * m.Inverse();
        for (var r = 0; r < 4; r++)
        for (var c = 0; c < 4; c++)
            Assert.Equal(r == c ? 1 : 0, id[r, c], 9);
    }

    [Fact]
    public void Rotation_about_z_turns_x_into_y()
    {
        var v = Matrix4d.Rotation(Vector3d.UnitZ, Math.PI / 2).TransformVector(Vector3d.UnitX);
        Assert.True(v.IsEqual(Vector3d.UnitY, 1e-12));
    }

    [Fact]
    public void LookAt_maps_target_onto_negative_z()
    {
        var view = Matrix4d.LookAt(new Vector3d(10, 0, 0), Vector3d.Zero, Vector3d.UnitZ);
        var p = view.TransformPoint(Vector3d.Zero);
        Assert.True(p.IsEqual(new Vector3d(0, 0, -10), 1e-12));
    }

    [Fact]
    public void Perspective_maps_near_and_far_to_clip_range()
    {
        var proj = Matrix4d.Perspective(Math.PI / 3, 1.5, 1, 100);
        Assert.Equal(-1, proj.TransformPoint(new Vector3d(0, 0, -1)).Z, 9);
        Assert.Equal(1, proj.TransformPoint(new Vector3d(0, 0, -100)).Z, 9);
    }
}
