interface IEnumerable<T> {
}

class List<T> : IEnumerable<T> {
}
class Array<T> : IEnumerable<T> {
    int Length;
}

interface IEnumerable<T> {
    bool Any();
    bool Contains(T x);
    int Count();
    IEnumerable<T> Distinct();
    T First();
    IEnumerable<T> Intersect(IEnumerable<T> v);
    T Last();
    IEnumerable<T> Skip(int count);
    IEnumerable<T> Take(int count);
    List<T> ToList();
    T[] ToArray();
}

class Action {
}
class ApplicationData {
}
class Assembly {
}
class AssemblyName {
}
class Attribute {
}
class bool {
}
class byte {
}
class Canvas {
}
class CanvasControl {
}
class CanvasDrawEventArgs {
}
class CanvasTextFormat {
}
class CanvasTextLayout {
}
class char {
}
class Clipboard {
}
class Color {
}
class Colors {
}
class CoreApplication {
}
class CoreCursor {
}
class CoreCursorType {
}
class CoreTextCompositionCompletedEventArgs {
}
class CoreTextCompositionSegment {
}
class CoreTextCompositionStartedEventArgs {
}
class CoreTextEditContext {
}
class CoreTextFormatUpdatingEventArgs {
}
class CoreTextLayoutRequestedEventArgs {
}
class CoreTextRange {
}
class CoreTextSelectionRequestedEventArgs {
}
class CoreTextSelectionUpdatingEventArgs {
}
class CoreTextServicesManager {
}
class CoreTextTextRequestedEventArgs {
}
class CoreTextTextUpdatingEventArgs {
}
class CoreVirtualKeyStates {
}
class CoreWindow {
}
class CustomAttributeData {
}
class DataPackage {
}
class DataPackageOperation {
}
class DataPackageView {
}
class DateTime {
}
class Debug {
}
class DesignMode {
}
class Dictionary<TKey, TValue> {
    public IEnumerable<TKey> Keys;
    public IEnumerable<TValue> Values;
    void Add(TKey key, TValue value);
    void Clear();
    bool ContainsKey(TKey key);
    public bool ContainsValue(TValue value);
    bool Remove(TKey key);
    bool TryGetValue(TKey key, out TValue value);
}
class Directory {
}
class DispatcherTimer {
}
class double {
}
class Encoding {
}
class Enumerable<T> : IEnumerable<T> {
    //bool Any();
    //T First();
    //T[] ToArray();
    //int Count();
    T Max();
}
class EventInfo {
}
class Exception {
}
class FieldInfo {
}
class File {
    string[] ReadAllLines(string path, Encoding encoding);
}
class float {
}
class Flyout {
}
class FocusState {
}
class FrameworkElement {
}
interface IEnumerator {
}
class int {
}
class KeyEventArgs {
}
class Language {
}
delegate bool Predicate<T>(T obj);
class List<T> {
    void Add(T item);
    void AddRange(IEnumerable<T> collection);
    void Clear();
    void CopyTo(int index, T[] array, int arrayIndex, int count);
    int Count;
    int FindIndex(Predicate<T> match);
    List<T> GetRange(int index, int count);
    int IndexOf(T item);
    void Insert(int index, T item);
    void InsertRange(int index, IEnumerable<T> collection);
    bool Remove(T item);
    void RemoveAt(int index);
    void RemoveRange(int index, int count);
    void Reverse();
    void Sort();
    //void Sort(Comparison<T> comparison);
    //T[] ToArray();
}
class MainPage {
}
class ManualResetEvent {
}
class Math {
}
class MethodInfo {
}
class object {
}
class Page {
}
class ParameterInfo {
}
class Path {
}
class Point {
}
class PointerEventArgs {
}
class PointerPoint {
}
class PointerRoutedEventArgs {
}
class PropertyInfo {
}
class RadioButton {
}
class Rect {
    bool Contains(Point point);
}
class RoutedEventArgs {
}
class ScrollViewer {
}
class ScrollViewerViewChangedEventArgs {
}
class short {
}
class Size {
}
class Stack<T> : IEnumerable<T> {
    void Clear();
    int Count;
    T Peek();
    T Pop();
    void Push(T item);
}
class StandardDataFormats {
}
class StorageFolder {
}
class string {
}
class StringWriter {
}
class Task {
}
class TimeSpan {
}
class Type {
    TypeInfo GetTypeInfo();
}
class TypeInfo {
}
class UnderlineType {
}
class UserControl {
}
class UTF8Encoding {
}
class VirtualKey {
}
class void {
}
class WebUtility {
}
class Window {
}
