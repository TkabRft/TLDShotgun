"""Safe loader for phased live Blender authoring. Importing has no scene effects."""
from pathlib import Path

def load():
    source = Path(__file__).with_name('source') / 'ammunition_assets.py'
    scope = {'__file__': str(source), '__name__': 'ammunition_builder'}
    exec(compile(source.read_text(encoding='utf-8-sig'), str(source), 'exec'), scope)
    return scope

if __name__ == '__main__':
    raise RuntimeError('Use the explicit live Blender phases documented in README.md.')
