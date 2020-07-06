using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OData.Edm;

namespace CrmOdataClient
{
    public class FilterEdmEntityModel : IEdmEntityType
    {
        private readonly IEdmEntityType _model;
        private readonly Hashtable _navigationEntityTypes;

        private IList<IEdmProperty> _filteredDeclaredProperties;

        public FilterEdmEntityModel(IEdmEntityType model, Hashtable navigationEntityTypes)
        {
            _model = model;
            _navigationEntityTypes = navigationEntityTypes;
            var edmProperties = _model.DeclaredProperties
                .Where(x =>
                {
                    if (x.PropertyKind == EdmPropertyKind.None ||
                        x.PropertyKind == EdmPropertyKind.Structural)
                        return true;

                    if (x.PropertyKind == EdmPropertyKind.Navigation)
                    {
                        if (EdmTypeSemantics.IsCollection(x.Type))
                        {
                            var collectionElementType = ExtensionMethods.AsElementType(x.Type.Definition) as IEdmEntityType;
                            var name = collectionElementType.Name;
                            return _navigationEntityTypes.ContainsKey(name);
                        }
                        else
                        {
                            var collectionElementType = ExtensionMethods.AsElementType(x.Type.Definition) as IEdmEntityType;
                            var name = collectionElementType.Name;
                            return _navigationEntityTypes.ContainsKey(name);
                        }
                    }

                    return false;
                });
            _filteredDeclaredProperties = new List<IEdmProperty>(edmProperties);
        }

        public EdmTypeKind TypeKind => _model.TypeKind;

        public IEdmProperty FindProperty(string name)
        {
            return _model.FindProperty(name);
        }

        public bool IsAbstract => _model.IsAbstract;
        public bool IsOpen => _model.IsOpen;
        public IEdmStructuredType BaseType => _model.BaseType;
        public IEnumerable<IEdmProperty> DeclaredProperties => _filteredDeclaredProperties.AsEnumerable();
        public string Name => _model.Name;
        public EdmSchemaElementKind SchemaElementKind => _model.SchemaElementKind;
        public string Namespace => _model.Namespace;
        public IEnumerable<IEdmStructuralProperty> DeclaredKey => _model.DeclaredKey;
        public bool HasStream => _model.HasStream;
    }
}