using System.Collections.Generic;
using System.Collections.Specialized;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModel;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test
{
    [TestFixture]
    internal class ResultsViewModelTest
    {
        [Test]
        public void GivenSameCountResults_WhenUpdated_ThenCollectionDoesNotReset()
        {
            var collection = new ResultsViewModel.ResultCollection();
            collection.Update([ViewModel("Answer.")]);

            var actions = new List<NotifyCollectionChangedAction>();
            collection.CollectionChanged += (_, args) => actions.Add(args.Action);

            collection.Update([ViewModel("Answer..")]);

            CollectionAssert.DoesNotContain(actions, NotifyCollectionChangedAction.Reset);
            CollectionAssert.Contains(actions, NotifyCollectionChangedAction.Replace);
            ClassicAssert.AreEqual("Answer..", collection[0].Result.Title);
        }

        [Test]
        public void GivenSameResultViewModelInstance_WhenUpdated_ThenCollectionDoesNotRaiseNoOpReplace()
        {
            var collection = new ResultsViewModel.ResultCollection();
            var viewModel = ViewModel("Answer.");
            collection.Update([viewModel]);

            var actions = new List<NotifyCollectionChangedAction>();
            collection.CollectionChanged += (_, args) => actions.Add(args.Action);

            collection.Update([viewModel]);

            CollectionAssert.IsEmpty(actions);
            ClassicAssert.AreSame(viewModel, collection[0]);
        }

        private static ResultViewModel ViewModel(string title)
        {
            return new ResultViewModel(
                new Result
                {
                    Title = title,
                    SubTitle = "thinking",
                    IcoPath = "Images\\display\\Shorty_Ani1.png",
                    Score = 100
                },
                new Settings());
        }
    }
}
