using System.Collections.ObjectModel;

namespace ScreenVideoCaptureWpf.Core.Extensions
{
    public static class CollectionExtension
    {
        /// <summary>
        /// Добавляет к существующей коллекции новые элементы
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <param name="array"></param>
        public static void AddMany<T>(this Collection<T> collection, T[] array)
        {
            foreach (T item in array)
            {
                collection.Add(item);
            }
        }

        /// <summary>
        /// Перезаписывает коллекцию новым массивом
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <param name="array"></param>
        public static void SetMany<T>(this Collection<T> collection, T[] array)
        {
            collection.Clear();
            collection.AddMany(array);
        }
    }
}
