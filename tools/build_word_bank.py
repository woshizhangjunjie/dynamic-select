import csv
import html
import io
import json
import re
import ssl
import unicodedata
import urllib.request
from pathlib import Path


BASE_DIR = Path(__file__).resolve().parent.parent
DATA_DIR = BASE_DIR / "Data"
OUTPUT_FILE = DATA_DIR / "high_school_words.json"

CPS_WORD_LIST_URL = "https://www.cpsenglish.com/article/506"
SUPPLEMENTAL_URLS = [
    "https://raw.githubusercontent.com/KyleBing/english-vocabulary/master/json_original/json-sentence/GaoZhong_2.json",
    "https://raw.githubusercontent.com/KyleBing/english-vocabulary/master/json_original/json-sentence/GaoZhong_3.json",
]
ECDICT_URL = "https://raw.githubusercontent.com/skywind3000/ECDICT/master/ecdict.csv"


def fetch_text(url: str, *, verify_ssl: bool = True) -> str:
    headers = {
        "User-Agent": "Mozilla/5.0",
        "Accept-Encoding": "identity",
    }
    request = urllib.request.Request(url, headers=headers)
    context = None if verify_ssl else ssl._create_unverified_context()
    with urllib.request.urlopen(request, context=context, timeout=120) as response:
        return response.read().decode("utf-8", errors="ignore")


def fetch_json(url: str) -> object:
    return json.loads(fetch_text(url))


def normalize_key(text: str) -> str:
    text = text.strip()
    text = text.replace("’", "'").replace("‘", "'").replace("（", "(").replace("）", ")")
    text = text.replace("“", '"').replace("”", '"')
    text = unicodedata.normalize("NFKD", text)
    text = "".join(ch for ch in text if not unicodedata.combining(ch))
    text = re.sub(r"\s+", " ", text)
    return text.lower().strip()


def strip_html_tags(fragment: str) -> str:
    fragment = re.sub(r"<br\s*/?>", "\n", fragment, flags=re.I)
    fragment = re.sub(r"</p>", "\n", fragment, flags=re.I)
    fragment = re.sub(r"<[^>]+>", " ", fragment)
    fragment = html.unescape(fragment)
    fragment = re.sub(r"\s+", " ", fragment)
    return fragment.strip()


def parse_curriculum_words() -> list[str]:
    # 该站证书已过期，这里只在词库构建脚本中放宽校验。
    page_html = fetch_text(CPS_WORD_LIST_URL, verify_ssl=False)
    container_match = re.search(r'<div class="text-fmt">(.*?)<div class="post-opt', page_html, flags=re.S)
    if not container_match:
        raise RuntimeError("未找到词表正文容器")

    paragraphs = re.findall(r"<p[^>]*>(.*?)</p>", container_match.group(1), flags=re.S)
    words: list[str] = []
    for paragraph_html in paragraphs:
        text = strip_html_tags(paragraph_html)
        if not text:
            continue
        if text.startswith("2017 ") or text.startswith("1.") or text.startswith("2."):
            continue
        if text.lstrip().startswith("附"):
            break

        if re.fullmatch(r"[▲ ]*[A-Z][▲ ]*", text):
            continue

        text = re.sub(r"\s+([*])", r"\1", text)
        text = re.sub(r"([A-Za-z.\-/)])\s+([*]{1,2})", r"\1\2", text)
        text = re.sub(r"\s+", " ", text).strip()
        words.extend(part.strip() for part in text.split(",") if part.strip())

    words = [item for item in words if "版课标" not in item and "英语词汇" not in item and "▲" not in item]
    if len(words) != 3000:
        raise RuntimeError(f"词表数量异常，期望 3000，实际 {len(words)}")
    return words


def extract_variants(token: str) -> list[str]:
    token = re.sub(r"\*+$", "", token).strip()
    token = token.replace("（", "(").replace("）", ")")
    variants: list[str] = []

    if token == "a/an":
        variants.extend(["a", "an"])

    core = re.sub(r"\([^)]*\)", "", token).strip()
    if core:
        variants.append(core)

    for group in re.findall(r"\(([^)]*)\)", token):
        for item in re.split(r"[,/;]| or ", group):
            item = item.strip()
            if re.fullmatch(r"[A-Za-z.\- ']+", item):
                variants.append(item)

    expanded: list[str] = []
    for item in variants:
        item = item.strip()
        if not item:
            continue
        expanded.append(item)
        lower_item = item.lower()
        if lower_item.endswith("ise"):
            expanded.append(item[:-3] + "ize")
        if lower_item.endswith("yse"):
            expanded.append(item[:-3] + "yze")
        if lower_item.endswith("our"):
            expanded.append(item[:-3] + "or")
        if lower_item.endswith("re") and len(item) > 4:
            expanded.append(item[:-2] + "er")
        if lower_item == "licence":
            expanded.append("license")
        if lower_item == "practise":
            expanded.append("practice")
        if lower_item == "café":
            expanded.append("cafe")
        if lower_item == "o’clock":
            expanded.append("o'clock")
        if lower_item.startswith("criterion"):
            expanded.append("criterion")

    deduplicated: list[str] = []
    seen: set[str] = set()
    for item in expanded:
        key = normalize_key(item)
        if key and key not in seen:
            seen.add(key)
            deduplicated.append(item)
    return deduplicated


def load_supplemental_lookup() -> dict[str, dict]:
    lookup: dict[str, dict] = {}
    for url in SUPPLEMENTAL_URLS:
        entries = fetch_json(url)
        for entry in entries:
            if not isinstance(entry, dict):
                continue
            key = normalize_key(str(entry.get("word", "")))
            if key and key not in lookup:
                lookup[key] = entry
    return lookup


def stream_ecdict_lookup(required_keys: set[str]) -> dict[str, dict]:
    headers = {
        "User-Agent": "Mozilla/5.0",
        "Accept-Encoding": "identity",
    }
    request = urllib.request.Request(ECDICT_URL, headers=headers)
    found: dict[str, dict] = {}
    with urllib.request.urlopen(request, timeout=300) as response:
        reader = csv.DictReader(io.TextIOWrapper(response, encoding="utf-8", newline=""))
        for row in reader:
            key = normalize_key(row.get("word", ""))
            if key in required_keys and key not in found:
                found[key] = row
            if len(found) == len(required_keys):
                break
    return found


def join_translations(entry: dict) -> str:
    values: list[str] = []
    for item in entry.get("translations", []):
        translation = str(item.get("translation", "")).strip()
        translation = translation.strip("；; ")
        if translation and translation not in values:
            values.append(translation)
    return "；".join(values)


def clean_ecdict_translation(text: str) -> str:
    text = text.replace("\r", "\n")
    text = re.sub(r"\[[^\]]+\]", "", text)
    lines = [line.strip("；; ") for line in text.splitlines()]
    lines = [line for line in lines if line]
    cleaned = "；".join(lines)
    cleaned = re.sub(r"\s+", " ", cleaned)
    cleaned = re.sub(r"[；;]{2,}", "；", cleaned)
    return cleaned.strip("；; ")


def format_phonetic(primary: str | None, secondary: str | None = None) -> str:
    primary = (primary or "").strip().strip("/")
    secondary = (secondary or "").strip().strip("/")
    if primary and secondary and primary != secondary:
        return f"英 /{primary}/ 美 /{secondary}/"
    if primary:
        return f"/{primary}/"
    if secondary:
        return f"/{secondary}/"
    return ""


def find_best_match(token: str, lookup: dict[str, dict]) -> tuple[str | None, dict | None]:
    for variant in extract_variants(token):
        key = normalize_key(variant)
        if key in lookup:
            return key, lookup[key]
    return None, None


def build_word_items() -> list[dict]:
    words = parse_curriculum_words()
    supplemental_lookup = load_supplemental_lookup()

    missing_keys: set[str] = set()
    for token in words:
        if find_best_match(token, supplemental_lookup)[1] is None:
            for variant in extract_variants(token):
                key = normalize_key(variant)
                if key:
                    missing_keys.add(key)

    ecdict_lookup = stream_ecdict_lookup(missing_keys)

    output: list[dict] = []
    unresolved: list[str] = []
    for index, token in enumerate(words):
        display_word = re.sub(r"\*+$", "", token).strip()
        word_key, supplemental_entry = find_best_match(token, supplemental_lookup)
        if not word_key:
            variants = extract_variants(token)
            word_key = normalize_key(variants[0] if variants else display_word)

        ecdict_entry = ecdict_lookup.get(word_key or "")

        meaning = ""
        meaning_source = ""
        phonetic = ""
        example = ""
        example_translation = ""

        if supplemental_entry:
            meaning = join_translations(supplemental_entry)
            phonetic = format_phonetic(supplemental_entry.get("uk"), supplemental_entry.get("us"))
            sentences = supplemental_entry.get("sentences", [])
            if sentences:
                example = str(sentences[0].get("sentence", "")).strip()
                example_translation = str(sentences[0].get("translation", "")).strip()
            if meaning:
                meaning_source = "kylebing-english-vocabulary"

        if not meaning and ecdict_entry:
            meaning = clean_ecdict_translation(str(ecdict_entry.get("translation", "")))
            if meaning:
                meaning_source = "ecdict"

        if not phonetic and ecdict_entry:
            phonetic = format_phonetic(str(ecdict_entry.get("phonetic", "")))

        if not meaning:
            unresolved.append(display_word)

        output.append(
            {
                "word": display_word,
                "wordKey": word_key or normalize_key(display_word),
                "phonetic": phonetic or None,
                "meaning": meaning or "",
                "example": example or None,
                "exampleTranslation": example_translation or None,
                "level": "high-school",
                "source": "curriculum-standard",
                "wordSource": "cpsenglish-2017-3000",
                "meaningSource": meaning_source or "none",
                "sortOrder": index,
            }
        )

    if unresolved:
        raise RuntimeError(f"仍有词条未补齐释义：{unresolved[:20]}")
    return output


def main() -> None:
    DATA_DIR.mkdir(parents=True, exist_ok=True)
    items = build_word_items()
    OUTPUT_FILE.write_text(json.dumps(items, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"词库生成完成，共 {len(items)} 条：{OUTPUT_FILE}")


if __name__ == "__main__":
    main()
