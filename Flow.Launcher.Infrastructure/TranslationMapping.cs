using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Infrastructure
{
    public class TranslationMapping
    {
        private bool constructed;

        // Asssuming one original item maps to multi translated items  
        // list[i] is the last translated index + 1 of original index i  
        private readonly List<int> originalToTranslated = new List<int>();

        public void AddNewIndex(int translatedIndex, int length)
        {
            if (constructed)
                throw new InvalidOperationException("Mapping shouldn't be changed after constructed");

            originalToTranslated.Add(translatedIndex + length);
        }

        public int MapToOriginalIndex(int translatedIndex)
        {
            int loc = originalToTranslated.BinarySearch(translatedIndex);

            return loc > 0 ? loc : ~loc;
        }

        public void endConstruct()
        {
            if (constructed)
                throw new InvalidOperationException("Mapping has already been constructed");
            constructed = true;
        }
    }
}
