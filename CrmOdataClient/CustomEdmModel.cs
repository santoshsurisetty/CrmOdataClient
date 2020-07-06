using System.Collections.Generic;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Vocabularies;

namespace CrmOdataClient
{
    public class CustomEdmModel : IEdmModel
    {
        private readonly IEdmModel _model;

        public CustomEdmModel(IEdmModel model)
        {
            _model = model;
        }

        public IEdmSchemaType FindDeclaredType(string qualifiedName)
        {
            return _model.FindDeclaredType(qualifiedName);
        }

        public IEnumerable<IEdmOperation> FindDeclaredBoundOperations(IEdmType bindingType)
        {
            return _model.FindDeclaredBoundOperations(bindingType);
        }

        public IEnumerable<IEdmOperation> FindDeclaredBoundOperations(string qualifiedName, IEdmType bindingType)
        {
            return _model.FindDeclaredBoundOperations(qualifiedName, bindingType);
        }

        public IEnumerable<IEdmOperation> FindDeclaredOperations(string qualifiedName)
        {
            return _model.FindDeclaredOperations(qualifiedName);
        }

        public IEdmTerm FindDeclaredTerm(string qualifiedName)
        {
            return _model.FindDeclaredTerm(qualifiedName);
        }

        public IEnumerable<IEdmVocabularyAnnotation> FindDeclaredVocabularyAnnotations(IEdmVocabularyAnnotatable element)
        {
            return _model.FindDeclaredVocabularyAnnotations(element);
        }

        public IEnumerable<IEdmStructuredType> FindDirectlyDerivedTypes(IEdmStructuredType baseType)
        {
            return _model.FindDirectlyDerivedTypes(baseType);
        }

        public IEnumerable<IEdmSchemaElement> SchemaElements { get; set; }
        public IEnumerable<IEdmVocabularyAnnotation> VocabularyAnnotations { get; set; }
        public IEnumerable<IEdmModel> ReferencedModels { get; set; }
        public IEnumerable<string> DeclaredNamespaces { get; set; }
        public IEdmDirectValueAnnotationsManager DirectValueAnnotationsManager { get; set; }
        public IEdmEntityContainer EntityContainer { get; set; }
    }
}