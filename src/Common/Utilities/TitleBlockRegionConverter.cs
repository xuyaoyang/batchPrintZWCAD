using System;

namespace ZwcadBatchPlot;

/// <summary>
/// 图框库字段坐标在「相对打印框」与「块局部绝对矩形」之间的互转。
/// 与 <see cref="BatchPlotCommands"/> 中 ToFrameRelative / ToFrameRightBottomRelative
/// 以及编辑流程 ResolveEditFieldRegion 保持同一套约定，避免第二套坐标系。
/// </summary>
public static class TitleBlockRegionConverter
{
    /// <summary>
    /// 将库内相对坐标还原为当前打印框下的块局部绝对矩形。
    /// CoordinateMode 取自<strong>源</strong>图框定义；referenceFrame 取<strong>当前</strong>对话框打印范围。
    /// </summary>
    public static LocalRectangle FromStoredRelative(
        LocalRectangle storedRegion,
        LocalRectangle referenceFrame,
        string? coordinateMode,
        LocalRectangle? recordedFrame = null)
    {
        if (!storedRegion.HasArea())
        {
            return new LocalRectangle();
        }

        if (string.Equals(coordinateMode, "Frame", StringComparison.OrdinalIgnoreCase))
        {
            return FromFrameRelative(storedRegion, referenceFrame, recordedFrame);
        }

        if (string.Equals(
                coordinateMode,
                TitleBlockDefinition.DynamicRightBottomCoordinateMode,
                StringComparison.OrdinalIgnoreCase))
        {
            return FromFrameRightBottomRelative(storedRegion, referenceFrame);
        }

        // Local：库内已是块局部绝对坐标；World 等其它模式由调用方自行变换。
        return Clone(storedRegion);
    }

    /// <summary>左下角锚点：相对打印框 MinX/MinY（固定图幅 Frame 模式）。</summary>
    public static LocalRectangle FromFrameRelative(LocalRectangle relativeRegion, LocalRectangle referenceFrame, LocalRectangle? recordedFrame = null)
    {
        if (!relativeRegion.HasArea()) return new LocalRectangle();
        var sx = recordedFrame?.HasArea() == true
            ? (referenceFrame.MaxX - referenceFrame.MinX) / (recordedFrame.MaxX - recordedFrame.MinX) : 1d;
        var sy = recordedFrame?.HasArea() == true
            ? (referenceFrame.MaxY - referenceFrame.MinY) / (recordedFrame.MaxY - recordedFrame.MinY) : 1d;
        return LocalRectangle.FromPoints(
            relativeRegion.MinX * sx + referenceFrame.MinX,
            relativeRegion.MinY * sy + referenceFrame.MinY,
            relativeRegion.MaxX * sx + referenceFrame.MinX,
            relativeRegion.MaxY * sy + referenceFrame.MinY);
    }

    /// <summary>仅等比例外框使用实时尺寸；不同长宽比的手动打印范围保持原定义。
    /// 两个矩形均在块定义坐标内，插入比例/旋转由调用方的 BlockTransform 处理一次。</summary>
    public static bool IsProportionalFrame(LocalRectangle recorded, LocalRectangle current)
    {
        if (!recorded.HasArea() || !current.HasArea()) return false;
        var sx = (current.MaxX - current.MinX) / (recorded.MaxX - recorded.MinX);
        var sy = (current.MaxY - current.MinY) / (recorded.MaxY - recorded.MinY);
        return Math.Abs(sx - sy) <= Math.Max(sx, sy) * 0.005;
    }

    /// <summary>
    /// 右下角锚点：横向相对 MaxX、纵向相对 MinY（可拉伸 FrameRightBottomDynamic）。
    /// </summary>
    public static LocalRectangle FromFrameRightBottomRelative(LocalRectangle relativeRegion, LocalRectangle referenceFrame)
    {
        return LocalRectangle.FromPoints(
            relativeRegion.MinX + referenceFrame.MaxX,
            relativeRegion.MinY + referenceFrame.MinY,
            relativeRegion.MaxX + referenceFrame.MaxX,
            relativeRegion.MaxY + referenceFrame.MinY);
    }

    public static LocalRectangle ToFrameRelative(LocalRectangle region, LocalRectangle referenceFrame)
    {
        return LocalRectangle.FromPoints(
            region.MinX - referenceFrame.MinX,
            region.MinY - referenceFrame.MinY,
            region.MaxX - referenceFrame.MinX,
            region.MaxY - referenceFrame.MinY);
    }

    public static LocalRectangle ToFrameRightBottomRelative(LocalRectangle region, LocalRectangle referenceFrame)
    {
        return LocalRectangle.FromPoints(
            region.MinX - referenceFrame.MaxX,
            region.MinY - referenceFrame.MinY,
            region.MaxX - referenceFrame.MaxX,
            region.MaxY - referenceFrame.MinY);
    }

    private static LocalRectangle Clone(LocalRectangle region)
    {
        return LocalRectangle.FromPoints(region.MinX, region.MinY, region.MaxX, region.MaxY);
    }
}
