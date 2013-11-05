﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Coevery.Common.Services;
using Coevery.Entities.Events;
using Coevery.Entities.Models;
using Coevery.Entities.ViewModels;
using Coevery;
using Coevery.ContentManagement;
using Coevery.ContentManagement.MetaData.Models;
using Coevery.ContentManagement.MetaData.Services;
using Coevery.Core.Settings.Metadata.Records;
using Coevery.Data;
using IContentDefinitionEditorEvents = Coevery.Entities.Settings.IContentDefinitionEditorEvents;

namespace Coevery.Entities.Services {
    public interface IContentMetadataService : IDependency {
        void CreateEntity(EditTypeViewModel sourceModel);
        IEnumerable<EntityMetadataPart> GetRawEntities();
        EntityMetadataPart GetEntity(int id);
        EntityMetadataPart GetEntity(string name);
        EntityMetadataPart GetDraftEntity(int id);
        EntityMetadataPart GetDraftEntity(string name);
        bool CheckEntityCreationValid(string name, string displayName);
        bool CheckEntityPublished(string name);
        bool CheckEntityDisplayValid(string name, string displayName);
        string ConstructEntityName(string entityName);
        string DeleteEntity(int id);

        IEnumerable<FieldMetadataRecord> GetFieldsList(int entityId);
        SettingsDictionary ParseSetting(string setting);
        string ConstructFieldName(string entityName, string displayName);
        bool CheckFieldCreationValid(EntityMetadataPart entity, string name, string displayName);
        void CreateField(EntityMetadataPart entity, AddFieldViewModel viewModel, IUpdateModel updateModel);
        bool DeleteField(string filedName,string entityName);
        void UpdateField(FieldMetadataRecord record, string displayName, IUpdateModel updateModel);
        void UpdateFieldSetting(FieldMetadataRecord record, EditTypeViewModel sourceModel);
    }

    public class ContentMetadataService : IContentMetadataService {
        private readonly ISettingsFormatter _settingsFormatter;
        private readonly IRepository<ContentFieldDefinitionRecord> _fieldDefinitionRepository;
        private readonly IContentDefinitionEditorEvents _contentDefinitionEditorEvents;
        private readonly IContentDefinitionService _contentDefinitionService;
        private readonly ISchemaUpdateService _schemaUpdateService;
        private readonly IEntityEvents _entityEvents;

        public ContentMetadataService(
            ICoeveryServices services,
            ISettingsFormatter settingsFormatter,
            IContentDefinitionService contentDefinitionService,
            ISchemaUpdateService schemaUpdateService,
            IEntityEvents entityEvents,
            IRepository<ContentFieldDefinitionRecord> fieldDefinitionRepository,
            IContentDefinitionEditorEvents contentDefinitionEditorEvents)
        {
            _contentDefinitionService = contentDefinitionService;
            _schemaUpdateService = schemaUpdateService;
            _entityEvents = entityEvents;
            _settingsFormatter = settingsFormatter;
            _fieldDefinitionRepository = fieldDefinitionRepository;
            _contentDefinitionEditorEvents = contentDefinitionEditorEvents;
            Services = services;
        }

        public ICoeveryServices Services { get; private set; }

        #region Entity Related

        public IEnumerable<EntityMetadataPart> GetRawEntities() {
            return Services.ContentManager.Query<EntityMetadataPart, EntityMetadataRecord>()
                .ForVersion(VersionOptions.Latest).List();
        }

        public bool CheckEntityCreationValid(string name, string displayName) {
            return !Services.ContentManager
                .Query<EntityMetadataPart>(VersionOptions.Latest, "EntityMetadata").List()
                .Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)
                          || string.Equals(x.DisplayName, displayName, StringComparison.OrdinalIgnoreCase));
        }

        public bool CheckEntityPublished(string name) {
            return GetEntity(name).HasPublished();
        }

        public bool CheckEntityDisplayValid(string name, string displayName) {
            return !Services.ContentManager
                .Query<EntityMetadataPart>(VersionOptions.Latest, "EntityMetadata").List()
                .Any(x => !string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)
                          && string.Equals(x.DisplayName, displayName, StringComparison.OrdinalIgnoreCase));
        }

        public EntityMetadataPart GetEntity(int id) {
            return Services.ContentManager.Get<EntityMetadataPart>(id, VersionOptions.Latest);
        }

        public EntityMetadataPart GetEntity(string name) {
            return Services.ContentManager
                .Query<EntityMetadataPart>(VersionOptions.Latest, "EntityMetadata")
                .List().FirstOrDefault(x => x.Name == name);
        }

        public EntityMetadataPart GetDraftEntity(int id) {
            return Services.ContentManager.Get<EntityMetadataPart>(id, VersionOptions.DraftRequired);
        }

        public EntityMetadataPart GetDraftEntity(string name) {
            var entity = GetEntity(name);
            return entity == null
                ? null
                : Services.ContentManager.Get<EntityMetadataPart>(entity.Id, VersionOptions.DraftRequired);
        }

        public string ConstructEntityName(string entityName) {
            var resultName = entityName;
            while (GetEntity(resultName) != null) {
                resultName = VersionName(resultName);
            }
            return resultName;
        }

        public void CreateEntity(EditTypeViewModel sourceModel) {
            var entityDraft = Services.ContentManager.New<EntityMetadataPart>("EntityMetadata");
            entityDraft.DisplayName = sourceModel.DisplayName;
            entityDraft.Name = sourceModel.Name;

            if (sourceModel.FieldType == "TextField")
            {
                var baseFieldSetting = new SettingsDictionary {
                    {"DisplayName", sourceModel.FieldLabel},
                    {"AddInLayout", bool.TrueString},
                    {"Storage", "Part"},
                {"TextFieldSettings.IsDispalyField", bool.TrueString},
                {"TextFieldSettings.Required", bool.TrueString},
                {"TextFieldSettings.ReadOnly", bool.TrueString},
                {"TextFieldSettings.AlwaysInLayout", bool.TrueString},
                {"TextFieldSettings.IsSystemField", bool.TrueString},
                {"TextFieldSettings.IsAudit", bool.FalseString}
                };
                entityDraft.FieldMetadataRecords.Add(new FieldMetadataRecord
                {
                    Name = sourceModel.FieldName,
                ContentFieldDefinitionRecord = FetchFieldDefinition("TextField"),
                    Settings = CompileSetting(baseFieldSetting)
                });
            }
            else if (sourceModel.FieldType == "ReferenceField")
            {
                var baseFieldSetting = new SettingsDictionary {
                    {"DisplayName", sourceModel.FieldLabel},
                    {"AddInLayout", bool.TrueString},
                    {"EntityName", sourceModel.Name},
                    {"ReferenceFieldSettings.ContentTypeName",sourceModel.ReferName},
                    {"ReferenceFieldSettings.RelationshipName",sourceModel.RelationName},
                    {"ReferenceFieldSettings.DisplayAsLink", bool.FalseString},
                    {"ReferenceFieldSettings.IsDispalyField", bool.TrueString},
                    {"ReferenceFieldSettings.HelpText", string.Empty},
                    {"ReferenceFieldSettings.Required", bool.TrueString},
                    {"ReferenceFieldSettings.ReadOnly", bool.TrueString},
                    {"ReferenceFieldSettings.AlwaysInLayout", bool.TrueString},
                    {"ReferenceFieldSettings.IsSystemField", bool.TrueString},
                    {"ReferenceFieldSettings.IsAudit", bool.FalseString}
                };
                entityDraft.FieldMetadataRecords.Add(new FieldMetadataRecord
                {
                    Name = sourceModel.FieldName,
                    ContentFieldDefinitionRecord = FetchFieldDefinition(sourceModel.FieldType),
                    Settings = CompileSetting(baseFieldSetting)
                });
            }
            Services.ContentManager.Create(entityDraft, VersionOptions.Draft);
        }

        public void UpdateFieldSetting(FieldMetadataRecord record, EditTypeViewModel sourceModel)
        {
            var settingsDictionary = ParseSetting(record.Settings);
            _contentDefinitionEditorEvents.UpdateFieldSettings(sourceModel.FieldType, sourceModel.FieldName,
                    sourceModel.Name, sourceModel.ReferName, sourceModel.RelationName, settingsDictionary);
            record.Settings = CompileSetting(settingsDictionary);

        }


        public string DeleteEntity(int id) {
            var entity = GetEntity(id);
            if (entity == null) {
                return "Invalid id";
            }
            foreach (var field in entity.FieldMetadataRecords) {
                _contentDefinitionEditorEvents.CustomDeleteAction(field.ContentFieldDefinitionRecord.Name, field.Name, ParseSetting(field.Settings));
            }
            var hasPublished = entity.HasPublished();

            if (hasPublished) {
                _schemaUpdateService.DropTable(entity.Name);
                _entityEvents.OnDeleting(entity.Name);
                _contentDefinitionService.RemoveType(entity.Name, true);
            }
            Services.ContentManager.Remove(entity.ContentItem);

            return null;
        }

        #endregion

        #region Field Related

        public IEnumerable<FieldMetadataRecord> GetFieldsList(int entityId) {
            return GetEntity(entityId).FieldMetadataRecords;
        }

        public SettingsDictionary ParseSetting(string setting) {
            return string.IsNullOrWhiteSpace(setting) 
                ? null
                : _settingsFormatter.Map(XElement.Parse(setting));
        }

        private string CompileSetting(SettingsDictionary settings) {
            return settings == null
                ? null
                : _settingsFormatter.Map(settings).ToString();
        }

        public bool CheckFieldCreationValid(EntityMetadataPart entity, string name, string displayName) {
            return !entity.FieldMetadataRecords.Any(
                field => string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase)
                         || string.Equals(ParseSetting(field.Settings)["DisplayName"], displayName, StringComparison.OrdinalIgnoreCase));
        }

        public string ConstructFieldName(string entityName, string displayName) {
            var entity = GetEntity(entityName);
            if (entity == null) {
                throw new ArgumentException("The entity doesn't exist: " + entityName);
            }
            var resultName = displayName;
            while (entity.FieldMetadataRecords.Any(x => String.Equals(resultName, x.Name, StringComparison.OrdinalIgnoreCase))) {
                resultName = VersionName(resultName);
            }
            return resultName;
        }

        public void CreateField(EntityMetadataPart entity, AddFieldViewModel viewModel, IUpdateModel updateModel) {
            var settingsDictionary = new SettingsDictionary();
            settingsDictionary["DisplayName"] = viewModel.DisplayName;
            settingsDictionary["AddInLayout"] = viewModel.AddInLayout.ToString();
            settingsDictionary["EntityName"] = entity.Name;
            var field = new FieldMetadataRecord {
                ContentFieldDefinitionRecord = FetchFieldDefinition(viewModel.FieldTypeName),
                Name = viewModel.Name
            };
            entity.FieldMetadataRecords.Add(field);
            _contentDefinitionEditorEvents.UpdateFieldSettings(viewModel.FieldTypeName, viewModel.Name, settingsDictionary, updateModel);
            field.Settings = CompileSetting(settingsDictionary);
            field.EntityMetadataRecord = entity.Record;
        }

        public void UpdateField(FieldMetadataRecord record, string displayName, IUpdateModel updateModel) {
            var settingsDictionary = ParseSetting(record.Settings);
            settingsDictionary["DisplayName"] = displayName;
            _contentDefinitionEditorEvents.UpdateFieldSettings(record.ContentFieldDefinitionRecord.Name, record.Name, settingsDictionary, updateModel);
            record.Settings = CompileSetting(settingsDictionary);
        }

        public bool DeleteField(string fieldName,string entityName) {
            var entity = GetDraftEntity(entityName);
            if (entity == null) {
                return false;
            }
            var field = entity.FieldMetadataRecords.FirstOrDefault(record => record.Name == fieldName);
            if (field == null) {
                return false;
            }

            _contentDefinitionEditorEvents.CustomDeleteAction(field.ContentFieldDefinitionRecord.Name, field.Name, ParseSetting(field.Settings));
            entity.FieldMetadataRecords.Remove(field);
            return true;
        }

        #region Field Private Methods

        private ContentFieldDefinitionRecord FetchFieldDefinition(string fieldType) {
            var baseFieldDefinition = _fieldDefinitionRepository.Get(def => def.Name == fieldType);
            if (baseFieldDefinition == null) {
                baseFieldDefinition = new ContentFieldDefinitionRecord {Name = fieldType};
                _fieldDefinitionRepository.Create(baseFieldDefinition);
            }
            return baseFieldDefinition;
        }

        #endregion

        #endregion

        private static string VersionName(string name) {
            int version;
            var nameParts = name.Split(new[] {'_'}, StringSplitOptions.RemoveEmptyEntries);

            if (nameParts.Length > 1 && int.TryParse(nameParts.Last(), out version)) {
                version = version > 0 ? ++version : 2;
                //this could unintentionally chomp something that looks like a version
                name = string.Join("_", nameParts.Take(nameParts.Length - 1));
            }
            else {
                version = 2;
            }
            return string.Format("{0}_{1}", name, version);
        }
    }
}