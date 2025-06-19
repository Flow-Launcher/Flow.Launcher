using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Infrastructure
{
    public class TranslationMapping
    {
        private bool constructed;

        private readonly List<int> originalIndexes = new();
        private readonly List<int> translatedIndexes = new();

        private int translatedLength = 0;

        public void AddNewIndex(int originalIndex, int translatedIndex, int length)
        {
            if (constructed)
                throw new InvalidOperationException("Mapping shouldn't be changed after constructed");

            originalIndexes.Add(originalIndex);
            translatedIndexes.Add(translatedIndex);
            translatedIndexes.Add(translatedIndex + length);
            translatedLength += length - 1;
        }

        public int MapToOriginalIndex(int translatedIndex)
        {
            if (translatedIndex > translatedIndexes.Last())
                return translatedIndex - translatedLength - 1;

            int lowerBound = 0;
            int upperBound = originalIndexes.Count - 1;

            int count = 0;

            // Corner case handle
            if (translatedIndex < translatedIndexes[0])
                return translatedIndex;

            if (translatedIndex > translatedIndexes.Last())
            {
                int indexDef = 0;
                for (int k = 0; k < originalIndexes.Count; k++)
                {
                    indexDef += translatedIndexes[k * 2 + 1] - translatedIndexes[k * 2];
                }

                return translatedIndex - indexDef - 1;
            }

            // Binary Search with Range
            for (int i = originalIndexes.Count / 2;; count++)
            {
                if (translatedIndex < translatedIndexes[i * 2])
                {
                    // move to lower middle
                    upperBound = i;
                    i = (i + lowerBound) / 2;
                }
                else if (translatedIndex > translatedIndexes[i * 2 + 1] - 1)
                {
                    lowerBound = i;
                    // move to upper middle
                    // due to floor of integer division, move one up on corner case
                    i = (i + upperBound + 1) / 2;
                }
                else
                {
                    return originalIndexes[i];
                }

                if (upperBound - lowerBound <= 1 &&
                    translatedIndex > translatedIndexes[lowerBound * 2 + 1] &&
                    translatedIndex < translatedIndexes[upperBound * 2])
                {
                    int indexDef = 0;

                    for (int j = 0; j < upperBound; j++)
                    {
                        indexDef += translatedIndexes[j * 2 + 1] - translatedIndexes[j * 2];
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
}
