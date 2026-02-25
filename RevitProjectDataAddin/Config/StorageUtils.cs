using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitProjectDataAddin
{
    public static class StorageUtils
    {
        public static readonly Guid SchemaGuid = new Guid("EDB80220-42D3-4DA8-8379-F6D0B09C2459");

        public static List<string> GetAllProjectNames(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(DataStorage))
                .Cast<DataStorage>()
                .Where(ds => ds.GetEntity(GetSchema()).IsValid())
                .Select(ds => ds.GetEntity(GetSchema()).Get<string>("ProjectName"))
                .ToList();
        }

        public static Schema GetSchema()
        {
            Schema schema = Schema.Lookup(SchemaGuid);
            if (schema != null) return schema;

            SchemaBuilder builder = new SchemaBuilder(SchemaGuid);
            builder.SetSchemaName("ProjectData");
            builder.AddSimpleField("Id", typeof(string)).SetDocumentation("Project Id");
            builder.AddSimpleField("ProjectName", typeof(string)).SetDocumentation("Project name");
            builder.AddSimpleField("JsonData", typeof(string)).SetDocumentation("Serialized project data");
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            return builder.Finish();
        }

        public static void SaveProject(Document doc, ProjectData data)
        {
            string json = System.Text.Json.JsonSerializer.Serialize(data);

            using (Transaction trans = new Transaction(doc, "Save Project Data"))
            {
                trans.Start();
                var ds = FindOrCreateDataStorage(doc, data.ProjectName);
                var entity = new Entity(GetSchema());

                entity.Set("Id", data.Id); // data.Id =  data.ProjectName nếu bạn dùng tên đó làm ID

                // Lưu ProjectName, đảm bảo là kiểu string
                entity.Set("ProjectName", data.ProjectName ?? string.Empty);

                // Lưu dữ liệu JSON
                entity.Set("JsonData", json);

                ds.SetEntity(entity);
                trans.Commit();
            }
        }

        public static void DeleteProject(Document doc, string projectName)
        {
            var storages = new FilteredElementCollector(doc)
                .OfClass(typeof(DataStorage))
                .Cast<DataStorage>();

            foreach (var storage in storages)
            {
                var schema = Schema.Lookup(SchemaGuid);
                if (schema == null) continue;

                var entity = storage.GetEntity(schema);
                if (!entity.IsValid()) continue;

                if (entity.Schema.ReadAccessLevel != AccessLevel.Public) continue;

                string id = entity.Get<string>("Id");
                if (id == projectName)
                {
                    using (Transaction tx = new Transaction(doc, "Delete Project"))
                    {
                        tx.Start();
                        doc.Delete(storage.Id);// xoa theo Element ID
                        tx.Commit();
                    }
                    break;
                }
            }
        }


        public static ProjectData LoadProject(Document doc, string name)
        {
            var ds = FindDataStorageByName(doc, name);
            if (ds == null) return null;

            var entity = ds.GetEntity(GetSchema());
            string json = entity.Get<string>("JsonData");
            return System.Text.Json.JsonSerializer.Deserialize<ProjectData>(json);
        }

        private static DataStorage FindOrCreateDataStorage(Document doc, string name)
        {
            var ds = FindDataStorageByName(doc, name);
            if (ds != null) return ds;

            ds = DataStorage.Create(doc);
            return ds;
        }

        private static DataStorage FindDataStorageByName(Document doc, string name)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(DataStorage))
                .Cast<DataStorage>()
                .FirstOrDefault(ds =>
                {
                    var entity = ds.GetEntity(GetSchema());
                    return entity.IsValid() && entity.Get<string>("ProjectName") == name;
                });
        }

    }
}
