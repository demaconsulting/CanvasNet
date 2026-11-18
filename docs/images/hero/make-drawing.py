"""Write drawing.vsdx, the Visio page of hero.svg: three boxes joined by two arrows.

Run `python make-drawing.py` in this folder; it needs only the Python standard library. The package is
the smallest one CanvasNet reads (no masters, no theme), and its entries carry a fixed date so that the
file is the same on every run.
"""

import zipfile

CONTENT_TYPES = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
</Types>
"""

PACKAGE_RELS = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.microsoft.com/visio/2010/relationships/document" Target="visio/document.xml"/>
</Relationships>
"""

DOCUMENT = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<VisioDocument xmlns="http://schemas.microsoft.com/office/visio/2012/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"/>
"""

DOCUMENT_RELS = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.microsoft.com/visio/2010/relationships/pages" Target="pages/pages.xml"/>
</Relationships>
"""

PAGE_WIDTH = 6.0
PAGE_HEIGHT = 4.0

PAGES = f"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Pages xmlns="http://schemas.microsoft.com/office/visio/2012/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <Page ID="0" Name="Page-1" NameU="Page-1">
    <PageSheet><Cell N="PageWidth" V="{PAGE_WIDTH}"/><Cell N="PageHeight" V="{PAGE_HEIGHT}"/></PageSheet>
    <Rel r:id="rId1"/>
  </Page>
</Pages>
"""

PAGES_RELS = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.microsoft.com/visio/2010/relationships/page" Target="page1.xml"/>
</Relationships>
"""


def box(shape_id, x, y, width, height, fill, line, text):
    """A filled, outlined rectangle whose lower-left corner is at (x, y), with centered white text."""
    return f"""    <Shape ID="{shape_id}" Type="Shape">
      <Cell N="PinX" V="{x}"/><Cell N="PinY" V="{y}"/><Cell N="Width" V="{width}"/><Cell N="Height" V="{height}"/>
      <Cell N="LocPinX" V="0"/><Cell N="LocPinY" V="0"/><Cell N="Angle" V="0"/>
      <Cell N="FillForegnd" V="{fill}"/><Cell N="FillPattern" V="1"/>
      <Cell N="LineColor" V="{line}"/><Cell N="LineWeight" V="0.03"/><Cell N="LinePattern" V="1"/>
      <Section N="Character"><Row IX="0"><Cell N="Color" V="#FFFFFF"/><Cell N="Size" V="0.22"/><Cell N="Style" V="1"/></Row></Section>
      <Section N="Paragraph"><Row IX="0"><Cell N="HorzAlign" V="1"/></Row></Section>
      <Section N="Geometry" IX="0">
        <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
        <Row T="LineTo" IX="2"><Cell N="X" V="{width}"/><Cell N="Y" V="0"/></Row>
        <Row T="LineTo" IX="3"><Cell N="X" V="{width}"/><Cell N="Y" V="{height}"/></Row>
        <Row T="LineTo" IX="4"><Cell N="X" V="0"/><Cell N="Y" V="{height}"/></Row>
        <Row T="LineTo" IX="5"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
      </Section>
      <Text>{text}</Text>
    </Shape>
"""


def arrow(shape_id, begin_x, begin_y, end_x, end_y):
    """A straight connector from (begin_x, begin_y) to (end_x, end_y) with an arrowhead at its end."""
    dx = end_x - begin_x
    dy = end_y - begin_y
    return f"""    <Shape ID="{shape_id}" Type="Shape">
      <Cell N="PinX" V="{begin_x}"/><Cell N="PinY" V="{begin_y}"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
      <Cell N="LocPinX" V="0"/><Cell N="LocPinY" V="0"/><Cell N="Angle" V="0"/>
      <Cell N="BeginX" V="{begin_x}"/><Cell N="BeginY" V="{begin_y}"/><Cell N="EndX" V="{end_x}"/><Cell N="EndY" V="{end_y}"/>
      <Cell N="LineColor" V="#334155"/><Cell N="LineWeight" V="0.03"/><Cell N="LinePattern" V="1"/>
      <Cell N="EndArrow" V="4"/><Cell N="EndArrowSize" V="2"/>
      <Section N="Geometry" IX="0">
        <Cell N="NoFill" V="1"/>
        <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
        <Row T="LineTo" IX="2"><Cell N="X" V="{dx}"/><Cell N="Y" V="{dy}"/></Row>
      </Section>
    </Shape>
"""


SHAPES = (
    box(1, 0.4, 2.3, 1.6, 1.0, "#2563EB", "#1E3A8A", "Client")
    + box(2, 2.2, 0.5, 1.6, 1.0, "#7C3AED", "#4C1D95", "Service")
    + box(3, 4.0, 2.3, 1.6, 1.0, "#DB2777", "#831843", "Store")
    + arrow(4, 1.2, 2.3, 2.6, 1.5)
    + arrow(5, 3.4, 1.5, 4.8, 2.3)
)

PAGE = f"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<PageContents xmlns="http://schemas.microsoft.com/office/visio/2012/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <Shapes>
{SHAPES}  </Shapes>
</PageContents>
"""

ENTRIES = [
    ("[Content_Types].xml", CONTENT_TYPES),
    ("_rels/.rels", PACKAGE_RELS),
    ("visio/document.xml", DOCUMENT),
    ("visio/_rels/document.xml.rels", DOCUMENT_RELS),
    ("visio/pages/pages.xml", PAGES),
    ("visio/pages/_rels/pages.xml.rels", PAGES_RELS),
    ("visio/pages/page1.xml", PAGE),
]

with zipfile.ZipFile("drawing.vsdx", "w", zipfile.ZIP_DEFLATED) as package:
    for name, content in ENTRIES:
        info = zipfile.ZipInfo(name, date_time=(2026, 1, 1, 0, 0, 0))
        info.compress_type = zipfile.ZIP_DEFLATED
        package.writestr(info, content.encode("utf-8"))
