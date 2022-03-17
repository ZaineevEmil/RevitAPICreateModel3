using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAPICreateModel3
{
    [TransactionAttribute(TransactionMode.Manual)]
    public class CreateModel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            #region Получение данных из проекта Revit

            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            List<Level> levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .OfType<Level>()
                .ToList();

            List<Wall> walls = new List<Wall>();

            Level level1 = GetLevel(levels, "Уровень 1");
            Level level2 = GetLevel(levels, "Уровень 2");
            #endregion

            #region Ввод исходных данных
            // ввести длину и ширину стен в мм
            double length = 10000;
            double width = 5000;
            List<Line> linesWalls = CreateLinesOfWalls(length, width);

            // ввести тип и размер двери
            string typeDoor = "дверь";
            string nameTypeOfDoor = "Одиночные-Щитовые";
            string sizeDoor = "0915 x 2032 мм";

            // ввести тип и размер окна, а также расстояние от окна до пола
            string typeWindow = "окно";
            string nameTypeOfWindow = "Фиксированные";
            string sizeWindow = "0610 x 1220 мм";
            double windowНeight = 915;

            // ввести тип и размер крыши, а также высоту в коньке
            string nameTypeOfRoof = "Базовая крыша";
            string sizeRoof = "Типовой - 400мм";
            double roofHeight = 1500;


            #endregion

            #region Команды, выполняющие построение в Revit
            using (var ts = new Transaction(doc, "Создание стен"))
            {
                ts.Start();
                foreach (Line line in linesWalls)
                {
                    CreateWalls(walls, doc, line, level1, level2);
                }

                AddDoorOrWindow(doc, level1, walls[0], typeDoor, nameTypeOfDoor, sizeDoor, windowНeight);

                for (int i = 1; i < 4; i++)
                {
                    AddDoorOrWindow(doc, level1, walls[i], typeWindow, nameTypeOfWindow, sizeWindow, windowНeight);
                }

                CreateRoof(doc, level2, length, walls[1], nameTypeOfRoof, sizeRoof, roofHeight);

                ts.Commit();
            }
            return Result.Succeeded;
            #endregion

        }

        #region Вспомогательные методы
        public Level GetLevel(List<Level> levels, string nameLevel)
        {
            Level level1 = levels
                .Where(x => x.Name.Equals(nameLevel))
                .FirstOrDefault();
            return level1;
        }

        public List<Line> CreateLinesOfWalls(double length, double width)
        {
            double lengthInch = UnitUtils.ConvertToInternalUnits(length, UnitTypeId.Millimeters);
            double widthInch = UnitUtils.ConvertToInternalUnits(width, UnitTypeId.Millimeters);
            double dx = lengthInch / 2;
            double dy = widthInch / 2;

            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dx, -dy, 0));
            points.Add(new XYZ(dx, -dy, 0));
            points.Add(new XYZ(dx, dy, 0));
            points.Add(new XYZ(-dx, dy, 0));
            points.Add(new XYZ(-dx, -dy, 0));

            List<Line> linesWalls = new List<Line>();
            for (int i = 0; i < 4; i++)
            {
                Line line = Line.CreateBound(points[i], points[i + 1]);
                linesWalls.Add(line);
            }
            return linesWalls;
        }

        public List<Wall> CreateWalls(List<Wall> walls, Document doc, Line line, Level level1, Level level2)
        {
            Wall wall = Wall.Create(doc, line, level1.Id, false);
            wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(level2.Id);
            walls.Add(wall);
            return walls;
        }

        public void AddDoorOrWindow(Document doc, Level level1, Wall wall, string type, string nameTypeOfDoorOrWindow, string sizeDoorOrWindow, double windowНeight)
        {
            FamilySymbol doorOrWindowType = null;

            if (type == "окно")
            {
                doorOrWindowType = new FilteredElementCollector(doc)
                  .OfClass(typeof(FamilySymbol))
                  .OfCategory(BuiltInCategory.OST_Windows)
                  .OfType<FamilySymbol>()
                  .Where(x => x.FamilyName.Equals(nameTypeOfDoorOrWindow))
                  .Where(x => x.Name.Equals(sizeDoorOrWindow))
                  .FirstOrDefault();
            }

            if (type == "дверь")
            {
                doorOrWindowType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfType<FamilySymbol>()
                .Where(x => x.FamilyName.Equals(nameTypeOfDoorOrWindow))
                .Where(x => x.Name.Equals(sizeDoorOrWindow))
                .FirstOrDefault();
            }

            LocationCurve wallCurve = wall.Location as LocationCurve;
            XYZ point1 = wallCurve.Curve.GetEndPoint(0);
            XYZ point2 = wallCurve.Curve.GetEndPoint(1);
            XYZ pointMiddle = (point1 + point2) / 2;
            XYZ pointMiddleDoorOrWindow = null;

            if (type == "окно")
            {
                double windowНeightInch = UnitUtils.ConvertToInternalUnits(windowНeight, UnitTypeId.Millimeters);
                pointMiddleDoorOrWindow = new XYZ(pointMiddle.X, pointMiddle.Y, pointMiddle.Z + windowНeightInch);
            }

            if (type == "дверь")
            {
                pointMiddleDoorOrWindow = pointMiddle;
            }

            if (!doorOrWindowType.IsActive)
                doorOrWindowType.Activate();

            doc.Create.NewFamilyInstance(pointMiddleDoorOrWindow, doorOrWindowType, wall, level1, StructuralType.NonStructural);
        }

        public void CreateRoof(Document doc, Level level, double length, Wall wall1, string nameTypeOfRoof, string sizeRoof, double roofHeight)
        {
            RoofType roofType = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>()
                .Where(x => x.FamilyName.Equals(nameTypeOfRoof))
                .Where(x => x.Name.Equals(sizeRoof))
                .FirstOrDefault();

            double wallWidth = wall1.Width;
            double dt = wallWidth / 2;
            double lengthInch = UnitUtils.ConvertToInternalUnits(length, UnitTypeId.Millimeters);

            double elevation = level.Elevation;

            LocationCurve wallCurve = wall1.Location as LocationCurve;
            XYZ point1 = wallCurve.Curve.GetEndPoint(0);
            XYZ pointRoof1 = new XYZ(point1.X + dt, point1.Y - dt, point1.Z + elevation);

            XYZ point2 = wallCurve.Curve.GetEndPoint(1);
            XYZ pointRoof2 = new XYZ(point2.X + dt, point2.Y + dt, point2.Z + elevation);

            XYZ pointMiddle = (point1 + point2) / 2;
            double roofНeightInch = UnitUtils.ConvertToInternalUnits(roofHeight, UnitTypeId.Millimeters);
            XYZ pointMiddleRoof = new XYZ(pointMiddle.X+dt, pointMiddle.Y, pointMiddle.Z + elevation + roofНeightInch);

            CurveArray curveArray = new CurveArray();
            curveArray.Append(Line.CreateBound(pointRoof1, pointMiddleRoof));
            curveArray.Append(Line.CreateBound(pointMiddleRoof, pointRoof2));

            ReferencePlane plane = doc.Create.NewReferencePlane2(pointRoof1, pointRoof2, pointMiddleRoof, doc.ActiveView);
            doc.Create.NewExtrusionRoof(curveArray, plane, level, roofType, 0, -(lengthInch + (2 * dt)));
        }

        #endregion
    }
}
