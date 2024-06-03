using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Infrastructure
{
    public class TranslationMapping
    {
        private bool constructed;

        private List<int> originalIndexs = new List<int>();
        private List<int> translatedIndexs = new List<int>();
        private int translatedLength = 0;

        public virtual void AddNewIndex(int originalIndex, int translatedIndex, int length)
        {
            if (constructed)
                throw new InvalidOperationException("Mapping shouldn't be changed after constructed");

            originalIndexs.Add(originalIndex);
            translatedIndexs.Add(translatedIndex);
            translatedIndexs.Add(translatedIndex + length);
            translatedLength += length - 1;
        }

        public virtual int MapToOriginalIndex(int translatedIndex)
        {
            if (translatedIndex > translatedIndexs.Last())
                return translatedIndex - translatedLength - 1;

            int lowerBound = 0;
            int upperBound = originalIndexs.Count - 1;

            int count = 0;

            // Corner case handle
            if (translatedIndex < translatedIndexs[0])
                return translatedIndex;
            if (translatedIndex > translatedIndexs.Last())
            {
                int indexDef = 0;
                for (int k = 0; k < originalIndexs.Count; k++)
                {
                    indexDef += translatedIndexs[k * 2 + 1] - translatedIndexs[k * 2];
                }

                return translatedIndex - indexDef - 1;
            }

            // Binary Search with Range
            for (int i = originalIndexs.Count / 2;; count++)
            {
                if (translatedIndex < translatedIndexs[i * 2])
                {
                    // move to lower middle
                    upperBound = i;
                    i = (i + lowerBound) / 2;
                }
                else if (translatedIndex > translatedIndexs[i * 2 + 1] - 1)
                {
                    lowerBound = i;
                    // move to upper middle
                    // due to floor of integer division, move one up on corner case
                    i = (i + upperBound + 1) / 2;
                }
                else
                    return originalIndexs[i];

                if (upperBound - lowerBound <= 1 &&
                    translatedIndex > translatedIndexs[lowerBound * 2 + 1] &&
                    translatedIndex < translatedIndexs[upperBound * 2])
                {
                    int indexDef = 0;

                    for (int j = 0; j < upperBound; j++)
                    {
                        indexDef += translatedIndexs[j * 2 + 1] - translatedIndexs[j * 2];
                    }

                    return translatedIndex - indexDef - 1;
                }
            }
        }

        public void endConstruct()
        {
            if (constructed)
                throw new InvalidOperationException("Mapping has already been constructed");
            constructed = true;
        }
    }

    public class TranslationMappingV2 : TranslationMapping
    {
        // Asssuming one original maps to multi translated
        private List<int> originalToTranslated = new();

        public override void AddNewIndex(int originalIndex, int translatedIndex, int length)
        {
            originalToTranslated.Add(translatedIndex + length);
        }

        public override int MapToOriginalIndex(int translatedIndex)
        {
            int loc = originalToTranslated.BinarySearch(translatedIndex);

            return loc > 0 ? loc : -loc - 1;
        }
    }
}
