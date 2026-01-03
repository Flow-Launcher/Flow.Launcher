using System;
using System.Collections.Generic;

namespace Flow.Launcher.Infrastructure
{
    public class TranslationMapping
    {
        private bool _isConstructed;

        // Assuming one original item maps to multi translated items
        // list[i] is the last translated index + 1 of original index i
        // Using short instead of int to save memory
        private List<short> _originalToTranslatedBuilder;
        private short[] _originalToTranslated;

        public TranslationMapping(int capacityHint = 16)
        {
            _originalToTranslatedBuilder = new List<short>(capacityHint);
        }

        public void AddNewIndex(int translatedIndex, int length)
        {
            if (_isConstructed)
                throw new InvalidOperationException("Mapping shouldn't be changed after construction");

            var value = translatedIndex + length;
            if (value > short.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(translatedIndex),
                    "Translation index exceeds maximum supported value (32,767)");

            _originalToTranslatedBuilder.Add((short)value);
        }

        public int MapToOriginalIndex(int translatedIndex)
        {
            if (_originalToTranslated == null)
                throw new InvalidOperationException("Mapping must be constructed before use");

            if (translatedIndex > short.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(translatedIndex),
                    "Translation index exceeds maximum supported value (32,767)");

            var searchResult = Array.BinarySearch(_originalToTranslated, (short)translatedIndex);
            return searchResult >= 0 ? searchResult + 1 : ~searchResult;
        }

        public void EndConstruct()
        {
            if (_isConstructed)
                throw new InvalidOperationException("Mapping has already been constructed");

            // Convert to array to save memory (no List overhead, no excess capacity)
            _originalToTranslated = _originalToTranslatedBuilder.ToArray();
            _originalToTranslatedBuilder = null; // Allow GC to collect the List
            _isConstructed = true;
        }
    }
}
