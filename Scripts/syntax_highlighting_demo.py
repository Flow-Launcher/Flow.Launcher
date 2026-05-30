# syntax_highlighting_demo.py - Ultra-compact syntax showcase
import math as m, re                      # Imports & Aliases
from datetime import datetime as dt        # Member import

G_VAR: float = 3.1415                     # Types & Constants
__version__ = "2.0.0"                      # Builtin variables

class DemoError(Exception): pass           # OOP & Exceptions

def deco(f):                               # Decorator
    return lambda *a, **k: f(*a, **k)

@deco
class Showcase:
    """Docstring for class definition."""
    def __init__(self, val: int = 42) -> None:
        self.val = val                     # Numbers & Attributes
        self._private = 0xAF               # Hexadecimal literal

    @property
    def value(self) -> int: return self.val

    def process(self, x: float) -> str:
        try:
            val = m.sqrt(self.val) / x if x else None
        except (ValueError, ZeroDivisionError) as err:
            raise DemoError(f"Error: {err}")
        else:
            # f-string, multi-line, escapes, raw regex, formatting
            return f"Result: {val:.2f}\n" + r"^\d+$"
        finally:
            print("Step executed.")
# Literals, collections, and control flow
def demo() -> None:
    active, done = True, False             # Booleans
    items = [1, 2, 3]                      # Lists
    mapping = {"a": 1, "b": 2}             # Dictionaries
    squares = {x**2 for x in items}        # Set Comprehensions

    for i, x in enumerate(items):
        if x % 2 == 0 or done: continue
        elif not active: break
        else: pass

if __name__ == "__main__":
    s = Showcase(16)
    print(s.process(2.5))
