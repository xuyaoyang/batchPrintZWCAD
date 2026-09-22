# 修订6：固定图框整体缩放回归

日期：2026-09-22。宿主：AutoCAD 2023 accoreconsole。输入全部为测试代码生成的图框，不含业务图纸。

- before.json：旧版同一测试12组中8组失败，失败场景为块定义内整体0.5倍或2倍缩放；仅插入比例变化的4组通过。
- after.json：最终修订6构建同一测试12组全部通过，每组检查打印边界4角、图号/图名提取、签章角点，共36项断言。
- 组合：块内部0.5/1/2倍，块插入1/2倍，0/90°旋转；模板有非零局部原点。
- 三平台解决方案构建：0警告、0错误。印章库既有13项模块检查通过。
- installer-self-test.txt：解包、三平台依赖、隔离注册模拟通过；未修改真实CAD注册项。
- 完整PDF/PNG/JPG打印回归在后台宿主未产出结果，日志停在STAMP_SMOKE重生成；不能据此认定输出打印已通过。日志仅保存在上级项目证据目录。
- 中望、AutoCAD 2025–2027此次未做宿主实测。原业务DWG未打开或保存，未代用户安装或重启CAD。

复现：先构建LA.BatchPlot.sln，再运行scripts/test-frame-scaling.ps1，传入DotNet、CoreConsole、任意合成FixtureDwg以及OneDrive之外的OutputDirectory。脚本只打开夹具副本；测试生成独立内存数据库。测试源码tests/FrameScaling/Commands.cs。

适用范围：Frame模式的等比例固定外框（长宽缩放差不超过0.5%）。模板已有相对字段利用录入PrintRegion尺寸换算，不修改图框库。可拉伸右下角锚点与历史Local/World规则保持原样。任意手工裁剪区域不能仅靠长宽比无歧义推断；此回归不声称覆盖这类重定义。
