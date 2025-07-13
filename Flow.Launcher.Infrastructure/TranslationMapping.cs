using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Infrastructure
{
    public class TranslationMapping
    {
        private bool _isConstructed;

        // Assuming one original item maps to multi translated items  
        // list[i] is the last translated index + 1 of original index i  
        private readonly List<int> _originalToTranslated = new();

        public void AddNewIndex(int translatedIndex, int length)
        {
            if (_isConstructed)
                throw new InvalidOperationException("Mapping shouldn't be changed after construction");

            _originalToTranslated.Add(translatedIndex + length);
        }

        public int MapToOriginalIndex(int translatedIndex)
        {
            var searchResult = _originalToTranslated.BinarySearch(translatedIndex);
            return searchResult >= 0 ? searchResult : ~searchResult;
        }

        public void EndConstruct()
        {
            if (_isConstructed)
                throw new InvalidOperationException("Mapping has already been constructed");
            _isConstructed = true;
        }
    }
}
