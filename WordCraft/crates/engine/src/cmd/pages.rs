//! Whole-page editing, as a PDF editor offers it: duplicate pages, move them (drag a page in the
//! Pages pane), copy, cut and paste them between other pages, delete them and add blank ones.
//!
//! A word processor's pages are where its text happens to break, so a page is the body content
//! from the point where its first line starts up to where the next page's does. Before pages
//! are moved or inserted, the page starts involved are *pinned*: a page beginning partway
//! through a paragraph splits the paragraph there, and every page's first paragraph is given
//! Page Break Before. Each page then is a run of whole top-level blocks that keeps starting a
//! new page wherever it goes, so pages reorder like a PDF's while the text stays editable.
//!
//! Pages are numbered from 1 as the Pages pane shows them (`page`), whatever page numbers the
//! sections display. `count` takes that many pages from `page` on.

use serde_json::{Value, json};
use wordcraft_doc::para::PAGE_BREAK;
use wordcraft_doc::edit::Fragment;
use wordcraft_doc::{Block, Document, Paragraph, Path, Pos, StoryRef};
use wordcraft_layout::{DocLayout, Placed};

use crate::{CmdError, CmdResult, CommandSpec, Selection, Session, p};

/// Most pages one command handles at a time (duplicating a range, for one).
const MAX_PAGES: usize = 10_000;

pub fn specs() -> Vec<CommandSpec> {
    vec![
        CommandSpec::new("page.list", "Pages", "View › Navigation Pane › Pages", list)
            .params(r#"{} → {"pages": [{"page", "number", "preview"}], "clipboard": bool (whether pages wait to be pasted)}"#)
            .pure(),
        CommandSpec::new("page.duplicate", "Duplicate Page", "Insert › Pages", duplicate)
            .params(r#"{"page"?: n (1-based, default: the caret's page), "count"?: n (pages from `page`, default 1)} — the copy goes straight after them"#),
        CommandSpec::new("page.copy", "Copy Page", "View › Navigation Pane › Pages", copy)
            .params(r#"{"page"?: n, "count"?: n} — for page.paste; the document doesn't change"#)
            .pure(),
        CommandSpec::new("page.cut", "Cut Page", "View › Navigation Pane › Pages", cut).params(r#"{"page"?: n, "count"?: n}"#),
        CommandSpec::new("page.paste", "Paste Page", "View › Navigation Pane › Pages", paste)
            .params(r#"{"before"?: n | "after"?: n (1-based page; default: after the caret's page)} — the copied or cut pages go in there as pages of their own"#)
            .when(|s| if s.page_clipboard.is_none() { Some("no pages copied") } else { None }),
        CommandSpec::new("page.delete", "Delete Page", "View › Navigation Pane › Pages", delete).params(r#"{"page"?: n, "count"?: n}"#),
        CommandSpec::new("page.move", "Move Page", "View › Navigation Pane › Pages", move_pages)
            .params(r#"{"page": n, "count"?: n, "to": n (1-based slot: the pages go before what is now page `to`; pages + 1 = the end)}"#),
        CommandSpec::new("page.insertBlank", "Insert Blank Page", "View › Navigation Pane › Pages", insert_blank)
            .params(r#"{"before"?: n | "after"?: n (default: after the caret's page)}"#),
    ]
}

/// Where a page's content starts: top-level body block and byte offset in it (0 for a table).
type Start = (usize, usize);

/// The layout pages are counted in: the screen's in Print Layout, else a print layout of its own.
fn page_layout(s: &mut Session) -> std::sync::Arc<DocLayout> {
    if s.view.mode == wordcraft_layout::ViewMode::Print && !s.view.read_mode { s.layout() } else { s.export_layout() }
}

/// The page the caret is on (0-based).
fn caret_page(s: &mut Session) -> usize {
    let l = s.layout();
    let in_body = s.sel.focus.story == StoryRef::Body;
    let here = if in_body { l.caret_on(&s.sel.focus, s.page_hint).map(|c| c.page) } else { None };
    here.unwrap_or(s.page_hint).min(l.pages.len().saturating_sub(1))
}

/// Where each page's own content starts, in page order; the end of the body follows the last.
/// A page that only continues a table from the page before starts after that table (a table
/// isn't split), so it may own nothing: then it starts where the next one does.
fn starts(doc: &Document, l: &DocLayout) -> Vec<Start> {
    let n = doc.body.len();
    let mut out: Vec<Start> = Vec::with_capacity(l.pages.len() + 1);
    let mut seen_table: Option<usize> = None;
    for (i, page) in l.pages.iter().enumerate() {
        let mut first: Option<(Vec<u32>, usize)> = None;
        for it in &page.items {
            let Placed::Lines { story: StoryRef::Body, path, para, l0, .. } = it else { continue };
            let Some(ln) = para.lines.get(*l0) else { continue };
            let key = (path.0.clone(), ln.start);
            if first.as_ref().is_none_or(|f| key < *f) {
                first = Some(key);
            }
        }
        let fallback = if i == 0 { 0 } else { page.first_block.min(n) };
        let mut st = match first {
            Some((path, off)) if path.len() == 1 => (path.first().map_or(fallback, |b| *b as usize), off),
            Some((path, _)) => {
                let k = path.first().map_or(fallback, |b| *b as usize);
                // Inside a table: the table's start if it begins here, else after it.
                if seen_table == Some(k) { (k + 1, 0) } else { (k, 0) }
            }
            None => (fallback, 0),
        };
        if i == 0 {
            st = (0, 0);
        }
        // Tables seen on this page: one that goes on over the next page belongs to this one.
        for it in &page.items {
            if let Placed::Lines { story: StoryRef::Body, path, .. } = it
                && path.0.len() > 1
                && let Some(k) = path.0.first()
            {
                seen_table = Some(*k as usize);
            }
        }
        // A paragraph ending in a page break: the page after starts with the next block.
        if let Some(Block::Para(p)) = doc.body.get(st.0).map(|b| &**b)
            && st.1 >= p.len()
            && st.1 > 0
        {
            st = (st.0 + 1, 0);
        }
        let st = (st.0.min(n), st.1);
        // Never before the page in front (a float or odd layout could suggest it).
        let st = match out.last() {
            Some(prev) if st < *prev => *prev,
            _ => st,
        };
        out.push(st);
    }
    if out.is_empty() {
        out.push((0, 0));
    }
    out.push((n, 0));
    out
}

/// The 0-based pages `page`, `page + count` from the params (default: the caret's page).
fn page_range(s: &mut Session, v: &Value, pages: usize) -> Result<(usize, usize), CmdError> {
    let first = match p::u64(v, "page") {
        Some(0) => return Err(CmdError::Params("`page` counts from 1".into())),
        Some(n) => (n as usize).saturating_sub(1),
        None => caret_page(s),
    };
    if first >= pages {
        return Err(CmdError::Params(format!("there is no page {} (the document has {pages})", first + 1)));
    }
    let count = p::u64(v, "count").map_or(1, |c| (c as usize).clamp(1, MAX_PAGES));
    Ok((first, (first + count).min(pages)))
}

/// The slot new pages go in (0 = before the first page, `pages` = at the end), from `before`
/// or `after` (default: after the caret's page).
fn slot(s: &mut Session, v: &Value, pages: usize) -> Result<usize, CmdError> {
    if let Some(b) = p::u64(v, "before") {
        return Ok((b as usize).saturating_sub(1).min(pages));
    }
    if let Some(a) = p::u64(v, "after") {
        return Ok((a as usize).min(pages));
    }
    Ok((caret_page(s) + 1).min(pages))
}

/// Make `st` a block boundary that starts a page: split the paragraph a page starts partway
/// through, then give the block there Page Break Before (see [`mark_start`]). Returns the
/// block index and how many blocks were added at or before it.
fn pin(doc: &mut Document, st: Start) -> Result<(usize, usize), CmdError> {
    let (k, off) = st;
    let before = doc.body.len();
    let mut b = k;
    if off > 0 {
        match doc.body.get(k).map(|b| &**b) {
            Some(Block::Para(p)) if off < p.len() => {
                let off = p.clamp(off);
                if off > 0 {
                    doc.split_paragraph(&Pos::body(k, off))?;
                    b = k + 1;
                }
            }
            Some(Block::Para(_)) => b = k + 1,
            _ => {}
        }
    }
    let b = mark_start(doc, b)?;
    Ok((b, doc.body.len().saturating_sub(before)))
}

/// Make block `b` start a new page: Page Break Before on a paragraph; before a table, a page
/// break ending the paragraph in front of it (a paragraph holding only the break when there is
/// none). Nothing for the first block or the end of the body. Returns `b`'s index afterwards.
fn mark_start(doc: &mut Document, b: usize) -> Result<usize, CmdError> {
    if b == 0 || b >= doc.body.len() {
        return Ok(b);
    }
    let kind = doc.body.get(b).map(|x| matches!(&**x, Block::Table(_)));
    match kind {
        Some(false) => {
            let p = doc.para_mut(StoryRef::Body, &Path::top(b))?;
            if p.props.page_break_before != Some(true) {
                p.props.page_break_before = Some(true);
                p.touch();
            }
            Ok(b)
        }
        Some(true) => {
            let prev_para = doc.body.get(b - 1).and_then(|x| x.as_para()).map(|p| (p.len(), p.text.ends_with(PAGE_BREAK), p.section.is_some()));
            match prev_para {
                Some((_, true, _)) | Some((_, _, true)) => Ok(b),
                Some((len, false, false)) => {
                    let props = doc.body.get(b - 1).and_then(|x| x.as_para()).map(|p| p.props_at(len).clone()).unwrap_or_default();
                    doc.insert_text(&Pos::body(b - 1, len), &PAGE_BREAK.to_string(), &props)?;
                    Ok(b)
                }
                None => {
                    let brk = Paragraph::with_text(&PAGE_BREAK.to_string(), Default::default());
                    doc.insert_block(StoryRef::Body, &Path::top(b), Block::Para(brk))?;
                    Ok(b + 1)
                }
            }
        }
        None => Ok(b),
    }
}

/// Pin the page starts `ks` (any order, repeats allowed); returns their block indices, in the
/// order given, valid after all the pins.
fn pin_all(doc: &mut Document, ks: &[Start]) -> Result<Vec<usize>, CmdError> {
    let mut order: Vec<usize> = (0..ks.len()).collect();
    order.sort_by(|a, b| ks.get(*b).cmp(&ks.get(*a)));
    let mut out = vec![0usize; ks.len()];
    let mut done: Vec<(Start, usize)> = Vec::new();
    for i in order {
        let Some(st) = ks.get(i).copied() else { continue };
        if let Some((_, b)) = done.iter().find(|(s, _)| *s == st) {
            if let Some(o) = out.get_mut(i) {
                *o = *b;
            }
            continue;
        }
        let (b, added) = pin(doc, st)?;
        // Blocks added here push every later (already pinned) start along.
        if added > 0 {
            for (_, x) in done.iter_mut() {
                *x += added;
            }
            for (j, o) in out.iter_mut().enumerate() {
                if j != i && ks.get(j).is_some_and(|s| *s > st) {
                    *o += added;
                }
            }
        }
        if let Some(o) = out.get_mut(i) {
            *o = b;
        }
        done.push((st, b));
    }
    Ok(out)
}

/// After pages were rearranged: the body ends with a paragraph and not with a page break (which
/// would leave a blank page last), and the first block doesn't keep a Page Break Before.
fn tidy(doc: &mut Document) {
    if doc.body.is_empty() || matches!(doc.body.last().map(|b| &**b), Some(Block::Table(_))) {
        doc.body.push(wordcraft_doc::para_block(Paragraph::new()));
    }
    let last = doc.body.len() - 1;
    if let Ok(p) = doc.para_mut(StoryRef::Body, &Path::top(last)) {
        let mut end = p.len();
        while end > 0 && p.text.get(..end).is_some_and(|t| t.ends_with(PAGE_BREAK)) {
            end -= PAGE_BREAK.len_utf8();
        }
        if end < p.len() {
            let len = p.len();
            let _ = p.delete(end, len);
            p.touch();
        }
    }
    if let Ok(p) = doc.para_mut(StoryRef::Body, &Path::top(0))
        && p.props.page_break_before == Some(true)
    {
        p.props.page_break_before = None;
        p.touch();
    }
}

/// Put the caret at the start of block `b`'s page and scroll there.
fn land(s: &mut Session, b: usize) {
    let b = b.min(s.doc.body.len().saturating_sub(1));
    let pos = match s.doc.body.get(b).map(|x| &**x) {
        Some(Block::Table(_)) => s.doc.para_paths(StoryRef::Body).into_iter().find(|p| p.0.first() == Some(&(b as u32))).map(|path| Pos { story: StoryRef::Body, path, off: 0 }),
        _ => None,
    };
    s.sel = Selection::caret(pos.unwrap_or_else(|| Pos::body(b, 0)));
    s.ui_requests.push(json!({"scrollTo": "caret"}));
}

/// The pages `first..end` as a fragment (the document isn't changed), without the page breaks
/// that ended them: wherever they are pasted, page starts are pinned again.
fn copy_pages(doc: &Document, st: &[Start], first: usize, end: usize) -> Result<Fragment, CmdError> {
    let (Some(a), Some(b)) = (st.get(first).copied(), st.get(end).copied()) else {
        return Err(CmdError::Params("no such page".into()));
    };
    if a >= b {
        return Err(CmdError::Failed(format!("page {} only continues a table from the page before it", first + 1)));
    }
    let mut f = doc.copy_body_span(a, b);
    if let Some(Block::Para(p)) = f.blocks.last_mut() {
        let mut stop = p.len();
        while stop > 0 && p.text.get(..stop).is_some_and(|t| t.ends_with(PAGE_BREAK)) {
            stop -= PAGE_BREAK.len_utf8();
        }
        if stop < p.len() && stop > 0 {
            let len = p.len();
            let _ = p.delete(stop, len);
        }
    }
    if f.blocks.is_empty() {
        return Err(CmdError::Failed("that page has nothing of its own to copy".into()));
    }
    Ok(f)
}

fn list(s: &mut Session, _: &Value) -> CmdResult {
    let l = page_layout(s);
    let st = starts(&s.doc, &l);
    let pages: Vec<Value> = l
        .pages
        .iter()
        .enumerate()
        .map(|(i, pg)| {
            let preview = st.get(i).and_then(|(k, off)| s.doc.body.get(*k).and_then(|b| b.as_para()).map(|p| (p, *off))).map_or(String::new(), |(p, off)| {
                let t = p.plain_text();
                let from = off.min(t.len());
                let from = (0..=from).rev().find(|o| t.is_char_boundary(*o)).unwrap_or(0);
                t.get(from..).unwrap_or("").chars().filter(|c| !c.is_control()).take(60).collect()
            });
            json!({"page": i + 1, "number": pg.number, "preview": preview})
        })
        .collect();
    Ok(json!({"pages": pages, "clipboard": s.page_clipboard.is_some()}))
}

fn duplicate(s: &mut Session, v: &Value) -> CmdResult {
    let l = page_layout(s);
    let (first, end) = page_range(s, v, l.pages.len())?;
    let st = starts(&s.doc, &l);
    let frag = copy_pages(&s.doc, &st, first, end)?;
    let (Some(a), Some(b)) = (st.get(first).copied(), st.get(end).copied()) else { return Err(CmdError::Params("no such page".into())) };
    let pinned = pin_all(&mut s.doc, &[a, b])?;
    let at = pinned.get(1).copied().unwrap_or(s.doc.body.len());
    let n = s.doc.insert_body_blocks(at, &frag)?;
    let at = mark_start(&mut s.doc, at)?;
    mark_start(&mut s.doc, at + n)?;
    tidy(&mut s.doc);
    land(s, at);
    s.page_hint = end;
    s.status = if end - first == 1 { format!("Page {} duplicated.", first + 1) } else { format!("Pages {}–{} duplicated.", first + 1, end) };
    Ok(json!({"page": end + 1, "pages": end - first}))
}

fn copy(s: &mut Session, v: &Value) -> CmdResult {
    let l = page_layout(s);
    let (first, end) = page_range(s, v, l.pages.len())?;
    let st = starts(&s.doc, &l);
    let frag = copy_pages(&s.doc, &st, first, end)?;
    let text = frag.clipboard_text();
    s.page_clipboard = Some(frag);
    s.status = if end - first == 1 { format!("Page {} copied.", first + 1) } else { format!("{} pages copied.", end - first) };
    Ok(json!({"pages": end - first, "text": text}))
}

fn cut(s: &mut Session, v: &Value) -> CmdResult {
    let r = copy(s, v)?;
    delete(s, v)?;
    let n = r.get("pages").and_then(Value::as_u64).unwrap_or(1);
    s.status = if n == 1 { "Page cut.".into() } else { format!("{n} pages cut.") };
    Ok(r)
}

fn paste(s: &mut Session, v: &Value) -> CmdResult {
    let frag = s.page_clipboard.clone().ok_or_else(|| CmdError::Disabled("page.paste: no pages copied".into()))?;
    let l = page_layout(s);
    let at_page = slot(s, v, l.pages.len())?;
    let st = starts(&s.doc, &l);
    let target = st.get(at_page).copied().unwrap_or((s.doc.body.len(), 0));
    let pinned = pin_all(&mut s.doc, &[target])?;
    let at = pinned.first().copied().unwrap_or(s.doc.body.len());
    let n = s.doc.insert_body_blocks(at, &frag)?;
    let at = mark_start(&mut s.doc, at)?;
    mark_start(&mut s.doc, at + n)?;
    tidy(&mut s.doc);
    land(s, at);
    s.page_hint = at_page;
    s.status = format!("Pasted as page {}.", at_page + 1);
    Ok(json!({"page": at_page + 1}))
}

fn delete(s: &mut Session, v: &Value) -> CmdResult {
    let l = page_layout(s);
    let pages = l.pages.len();
    let (first, end) = page_range(s, v, pages)?;
    let st = starts(&s.doc, &l);
    let (Some(a), Some(b)) = (st.get(first).copied(), st.get(end).copied()) else { return Err(CmdError::Params("no such page".into())) };
    if a >= b {
        return Err(CmdError::Failed(format!("page {} only continues a table from the page before it", first + 1)));
    }
    let pinned = pin_all(&mut s.doc, &[a, b])?;
    let (ka, kb) = (pinned.first().copied().unwrap_or(0), pinned.get(1).copied().unwrap_or(s.doc.body.len()));
    remove_blocks(&mut s.doc, ka, kb);
    // What follows starts a page where the deleted ones did.
    mark_start(&mut s.doc, ka)?;
    tidy(&mut s.doc);
    land(s, ka);
    s.page_hint = first.min(pages.saturating_sub(end - first + 1));
    s.status = if end - first == 1 { format!("Page {} deleted.", first + 1) } else { format!("Pages {}–{} deleted.", first + 1, end) };
    Ok(json!({"deleted": end - first}))
}

/// Remove top-level blocks `ka..kb`. A section break among them moves to the paragraph in front
/// (when it has none), so the section layout before the deleted pages is kept.
fn remove_blocks(doc: &mut Document, ka: usize, kb: usize) -> Vec<Block> {
    let kb = kb.min(doc.body.len());
    if ka >= kb {
        return Vec::new();
    }
    let removed: Vec<Block> = doc.body.drain(ka..kb).map(|b| std::sync::Arc::try_unwrap(b).unwrap_or_else(|a| (*a).clone())).collect();
    let sect = removed.iter().rev().find_map(|b| b.as_para().and_then(|p| p.section.clone()));
    if let Some(sect) = sect
        && ka > 0
        && let Ok(p) = doc.para_mut(StoryRef::Body, &Path::top(ka - 1))
        && p.section.is_none()
    {
        p.section = Some(sect);
        p.touch();
    }
    if doc.body.is_empty() {
        doc.body.push(wordcraft_doc::para_block(Paragraph::new()));
    }
    removed
}

fn move_pages(s: &mut Session, v: &Value) -> CmdResult {
    let l = page_layout(s);
    let pages = l.pages.len();
    if p::u64(v, "page").is_none() {
        return Err(CmdError::Params("`page` (the page to move) is required".into()));
    }
    let (first, end) = page_range(s, v, pages)?;
    let to = p::u64(v, "to").ok_or_else(|| CmdError::Params("`to` (where the page goes) is required".into()))?;
    let to = (to as usize).saturating_sub(1).min(pages);
    if to >= first && to <= end {
        // Dropped where it already is.
        return Ok(json!({"page": first + 1, "moved": false}));
    }
    let st = starts(&s.doc, &l);
    let (Some(a), Some(b), Some(t)) = (st.get(first).copied(), st.get(end).copied(), st.get(to).copied()) else {
        return Err(CmdError::Params("no such page".into()));
    };
    if a >= b {
        return Err(CmdError::Failed(format!("page {} only continues a table from the page before it", first + 1)));
    }
    let pinned = pin_all(&mut s.doc, &[a, b, t])?;
    let (ka, kb, kt) = (pinned.first().copied().unwrap_or(0), pinned.get(1).copied().unwrap_or(0), pinned.get(2).copied().unwrap_or(0));
    if ka >= kb {
        return Ok(json!({"page": first + 1, "moved": false}));
    }
    // Whole blocks move (their text boxes with them); nothing is copied.
    let moved: Vec<std::sync::Arc<Block>> = s.doc.body.drain(ka..kb).collect();
    let n = moved.len();
    let at = if kt > kb { kt - n } else { kt };
    let at = at.min(s.doc.body.len());
    for (i, b) in moved.into_iter().enumerate() {
        s.doc.body.insert(at + i, b);
    }
    // Where the pages were, what followed them now starts a page; so do they where they went.
    let gap = if kt > kb { ka } else { ka + n };
    mark_start(&mut s.doc, gap)?;
    let at = mark_start(&mut s.doc, at)?;
    mark_start(&mut s.doc, at + n)?;
    tidy(&mut s.doc);
    land(s, at);
    let new_first = if to > end { to - (end - first) } else { to };
    s.page_hint = new_first;
    s.status = format!("Moved to page {}.", new_first + 1);
    Ok(json!({"page": new_first + 1, "moved": true}))
}

fn insert_blank(s: &mut Session, v: &Value) -> CmdResult {
    let l = page_layout(s);
    let at_page = slot(s, v, l.pages.len())?;
    let st = starts(&s.doc, &l);
    let target = st.get(at_page).copied().unwrap_or((s.doc.body.len(), 0));
    let pinned = pin_all(&mut s.doc, &[target])?;
    let at = pinned.first().copied().unwrap_or(s.doc.body.len());
    // The empty paragraph takes the formatting of the text in front of it, as a typed one would.
    let mut blank = Paragraph::new();
    if let Some(prev) = at.checked_sub(1).and_then(|k| s.doc.body.get(k)).and_then(|b| b.as_para()) {
        blank.props.style = prev.props.style.clone();
        blank.mark = prev.mark.clone();
    }
    s.doc.insert_block(StoryRef::Body, &Path::top(at), Block::Para(blank))?;
    let at = mark_start(&mut s.doc, at)?;
    mark_start(&mut s.doc, at + 1)?;
    tidy(&mut s.doc);
    land(s, at);
    s.page_hint = at_page;
    Ok(json!({"page": at_page + 1}))
}

#[cfg(test)]
mod tests {
    use serde_json::{Value, json};
    use wordcraft_doc::{Document, Path, StoryRef};

    use crate::Session;

    /// A document of one short paragraph per page (each after the first with Page Break Before).
    fn pages(texts: &[&str]) -> Session {
        let mut d = Document::from_text(&texts.join("\n"));
        for i in 1..texts.len() {
            d.para_mut(StoryRef::Body, &Path::top(i)).unwrap().props.page_break_before = Some(true);
        }
        Session::new(d)
    }

    /// The text each page starts with.
    fn firsts(s: &mut Session) -> Vec<String> {
        let v = s.run("page.list", &json!({})).unwrap();
        v["pages"].as_array().unwrap().iter().map(|p| p["preview"].as_str().unwrap().to_string()).collect()
    }

    #[test]
    fn duplicate_puts_a_copy_straight_after() {
        let mut s = pages(&["One", "Two", "Three"]);
        assert_eq!(firsts(&mut s), ["One", "Two", "Three"]);
        s.run("page.duplicate", &json!({"page": 2})).unwrap();
        assert_eq!(firsts(&mut s), ["One", "Two", "Two", "Three"]);
        // The first page too: its copy starts a page of its own.
        s.run("page.duplicate", &json!({"page": 1})).unwrap();
        assert_eq!(firsts(&mut s), ["One", "One", "Two", "Two", "Three"]);
        // A run of pages.
        s.run("page.duplicate", &json!({"page": 4, "count": 2})).unwrap();
        assert_eq!(firsts(&mut s), ["One", "One", "Two", "Two", "Three", "Two", "Three"]);
        // One undo step each.
        s.run("edit.undo", &json!({})).unwrap();
        s.run("edit.undo", &json!({})).unwrap();
        s.run("edit.undo", &json!({})).unwrap();
        assert_eq!(firsts(&mut s), ["One", "Two", "Three"]);
    }

    #[test]
    fn move_reorders_pages_both_ways() {
        let mut s = pages(&["One", "Two", "Three", "Four"]);
        s.run("page.move", &json!({"page": 4, "to": 1})).unwrap();
        assert_eq!(firsts(&mut s), ["Four", "One", "Two", "Three"]);
        // To the end: the slot after the last page.
        s.run("page.move", &json!({"page": 1, "to": 5})).unwrap();
        assert_eq!(firsts(&mut s), ["One", "Two", "Three", "Four"]);
        s.run("page.move", &json!({"page": 1, "count": 2, "to": 4})).unwrap();
        assert_eq!(firsts(&mut s), ["Three", "One", "Two", "Four"]);
        // Dropped where it is: nothing changes.
        let r = s.run("page.move", &json!({"page": 2, "to": 2})).unwrap();
        assert_eq!(r["moved"], json!(false));
        assert_eq!(firsts(&mut s), ["Three", "One", "Two", "Four"]);
        // The first page has no Page Break Before left over; the moved ones keep starting pages.
        let first = s.doc.body.first().unwrap().as_para().unwrap();
        assert_ne!(first.props.page_break_before, Some(true));
    }

    #[test]
    fn copy_and_paste_pages_between_others() {
        let mut s = pages(&["One", "Two", "Three"]);
        let before = s.doc.clone();
        s.run("page.copy", &json!({"page": 1})).unwrap();
        assert_eq!(s.doc, before, "copying a page doesn't change the document");
        s.run("page.paste", &json!({"after": 2})).unwrap();
        assert_eq!(firsts(&mut s), ["One", "Two", "One", "Three"]);
        s.run("page.paste", &json!({"before": 1})).unwrap();
        assert_eq!(firsts(&mut s), ["One", "One", "Two", "One", "Three"]);
        s.run("page.paste", &json!({"after": 5})).unwrap();
        assert_eq!(firsts(&mut s), ["One", "One", "Two", "One", "Three", "One"]);
    }

    #[test]
    fn cut_then_paste_moves_and_delete_removes() {
        let mut s = pages(&["One", "Two", "Three"]);
        s.run("page.cut", &json!({"page": 1})).unwrap();
        assert_eq!(firsts(&mut s), ["Two", "Three"]);
        s.run("page.paste", &json!({"after": 2})).unwrap();
        assert_eq!(firsts(&mut s), ["Two", "Three", "One"]);
        s.run("page.delete", &json!({"page": 2})).unwrap();
        assert_eq!(firsts(&mut s), ["Two", "One"]);
        s.run("page.delete", &json!({"page": 1, "count": 2})).unwrap();
        assert_eq!(s.layout().pages.len(), 1);
        assert_eq!(s.doc.plain_text(StoryRef::Body).trim(), "");
    }

    #[test]
    fn blank_pages_go_in_between() {
        let mut s = pages(&["One", "Two"]);
        s.run("page.insertBlank", &json!({"after": 1})).unwrap();
        assert_eq!(firsts(&mut s), ["One", "", "Two"]);
        s.run("page.insertBlank", &json!({"after": 3})).unwrap();
        assert_eq!(firsts(&mut s).len(), 4);
    }

    #[test]
    fn a_page_that_starts_partway_through_a_paragraph() {
        // One long paragraph over several pages: duplicating its second page splits it there.
        let long = "word ".repeat(3000);
        let mut s = Session::new(Document::from_text(&format!("{long}\nEnd")));
        let n = s.layout().pages.len();
        assert!(n >= 3, "{n} pages");
        let page2 = firsts(&mut s)[1].clone();
        s.run("page.duplicate", &json!({"page": 2})).unwrap();
        let after = firsts(&mut s);
        assert!(after.len() >= n + 1, "{n} → {}", after.len());
        assert_eq!(after.get(2).map(|t| t.split_whitespace().next()), Some(page2.split_whitespace().next()));
        let words = |s: &Session| s.doc.plain_text(StoryRef::Body).split_whitespace().count();
        assert!(words(&s) > 3001, "the copy adds its words");
        // Moving the last page to the front keeps every word.
        let total = words(&s);
        let last = s.layout().pages.len();
        s.run("page.move", &json!({"page": last, "to": 1})).unwrap();
        assert_eq!(words(&s), total);
    }

    #[test]
    fn page_breaks_ending_pages_leave_no_blank_pages() {
        // Pages ended by a typed page break (the way most documents break pages).
        let brk = wordcraft_doc::para::PAGE_BREAK;
        let mut s = Session::new(Document::from_text(&format!("One{brk}\nTwo{brk}\nThree")));
        assert_eq!(firsts(&mut s), ["One", "Two", "Three"]);
        s.run("page.move", &json!({"page": 1, "to": 4})).unwrap();
        assert_eq!(firsts(&mut s), ["Two", "Three", "One"]);
        s.run("page.duplicate", &json!({"page": 3})).unwrap();
        assert_eq!(firsts(&mut s), ["Two", "Three", "One", "One"]);
    }

    #[test]
    fn junk_params_are_refused_not_fatal() {
        let mut s = pages(&["One", "Two"]);
        assert!(s.run("page.paste", &json!({})).is_err(), "nothing copied yet");
        assert!(s.run("page.duplicate", &json!({"page": 0})).is_err());
        assert!(s.run("page.delete", &json!({"page": 99})).is_err());
        assert!(s.run("page.move", &json!({"page": 1})).is_err(), "`to` is required");
        for v in [json!({"page": 1e300, "to": -4}), json!({"page": "x", "count": 1e20}), json!({"before": 1e30}), json!(null), json!([1, 2])] {
            for id in ["page.duplicate", "page.copy", "page.cut", "page.delete", "page.move", "page.insertBlank", "page.list"] {
                let _ = s.run(id, &v);
            }
        }
        let n: Value = s.run("page.list", &json!({})).unwrap();
        assert!(!n["pages"].as_array().unwrap().is_empty());
    }
}
