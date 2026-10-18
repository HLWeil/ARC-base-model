"""Native call syntax; persistence behavior lives entirely in the F# core."""
from __future__ import annotations

from weakref import WeakValueDictionary
from ._impl.session import Session as _Session
from ._impl.ARCtrl.arc import ARC as _ARC

_instances = WeakValueDictionary()
_groups = frozenset(("Entity", "Dataset", "Process", "Sample", "Data", "Recipe", "Annotation",
                     "FormalParameter", "DefinedTerm", "DefinedTermSet", "Descriptor", "Agent",
                     "Organization", "ScholarlyArticle", "History"))


def _raw(value):
    return value._target if isinstance(value, _Facade) else value


def _wrap(value, kind):
    previous = _instances.get(id(value))
    if previous is not None:
        return previous
    result = object.__new__(kind)
    object.__setattr__(result, "_target", value)
    object.__setattr__(result, "_members", {})
    _instances[id(value)] = result
    return result


class _Facade:
    __slots__ = ("_target", "_members", "__weakref__")

    @property
    def __class__(self):
        # Keep isinstance checks transparent, like the JS Proxy boundary.
        return self._target.__class__

    def __init__(self):
        raise TypeError("Use the public factory methods")

    def __getattr__(self, name):
        if name in self._members:
            return self._members[name]
        value = getattr(self._target, name)
        if name in _groups:
            self._members[name] = _wrap(value, _Group)
            return self._members[name]
        if name == "ManagedGraph":
            self._members[name] = _wrap(value, ManagedGraph)
            return self._members[name]
        if not callable(value):
            return value
        if isinstance(self, _Group) and name == "upsert":
            def upsert(revision, entity, *, recursive=False):
                if type(recursive) is not bool:
                    raise TypeError("recursive must be a bool")
                return value(revision, entity, recursive)
            self._members[name] = upsert
            return upsert
        if isinstance(self, _Group) and name == "delete":
            def delete(revision, entity_id, *, cascade=False):
                if type(cascade) is not bool:
                    raise TypeError("cascade must be a bool")
                return value(revision, entity_id, cascade)
            self._members[name] = delete
            return delete
        def call(*args, **kwargs):
            result = value(*(_raw(argument) for argument in args),
                           **{key: _raw(argument) for key, argument in kwargs.items()})
            if isinstance(self, Session) and name in ("create_arc", "open_arc", "import_yaml"):
                return _wrap(result, ManagedGraph)
            return result
        self._members[name] = call
        return call

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.close()


class _Group(_Facade):
    __slots__ = ()


class ManagedGraph(_Facade):
    __slots__ = ()


class Session(_Facade):
    __slots__ = ()

    @staticmethod
    def create_in_memory():
        return _wrap(_Session.create_in_memory(), Session)

    @staticmethod
    def create_file(path):
        return _wrap(_Session.create_file(path), Session)

    @staticmethod
    def open_file(path):
        return _wrap(_Session.open_file(path), Session)


class ARC(_Facade):
    __slots__ = ()

    @staticmethod
    def bind(graph, folder):
        return _wrap(_ARC.bind(_raw(graph), folder), ARC)

    @staticmethod
    def from_graph(graph):
        return _wrap(_ARC.from_graph(_raw(graph)), ARC)

    @staticmethod
    def create(folder, root):
        return _wrap(_ARC.create(folder, root), ARC)

    @staticmethod
    def open_folder(folder, source):
        return _wrap(_ARC.open_folder(folder, source), ARC)

    @staticmethod
    def import_folder(session, folder):
        return _wrap(_ARC.import_folder(_raw(session), folder), ARC)
