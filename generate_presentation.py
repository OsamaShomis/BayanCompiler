# -*- coding: utf-8 -*-
import sys, re, os, base64

# Read university logo as base64
logo_path = r'e:\BayanCompiler\ibb_univ_logo.png'
with open(logo_path, 'rb') as f:
    logo_b64 = base64.b64encode(f.read()).decode('ascii')
logo_src = f"data:image/png;base64,{logo_b64}"

slides_html = []

css = """<!DOCTYPE html>
<html lang="ar" dir="rtl">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>مترجم لغة بيان — Bayan Compiler Presentation</title>
  <link rel="preconnect" href="https://fonts.googleapis.com">
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
  <link href="https://fonts.googleapis.com/css2?family=IBM+Plex+Mono:ital,wght@0,300;0,400;0,500;0,600;0,700;1,400&family=IBM+Plex+Sans+Arabic:wght@300;400;500;600;700&display=swap" rel="stylesheet" />
  <style>
    :root {
      --cream: #FBF9F5;
      --warm: #FAF8F4;
      --soft: #F4EFEB;
      --card-bg: #FFFFFF;
      --beige: #E6DDCF;
      --beige2: #D4C7B2;
      --navy: #152238;
      --navy2: #243554;
      --navy-light: #395079;
      --copper: #C8732A;
      --copper2: #E08538;
      --copper3: #F0DBC4;
      --text-main: #2A241C;
      --text-muted: #6B6255;
      --fm: 'IBM Plex Mono', monospace;
      --fa: 'IBM Plex Sans Arabic', sans-serif;
    }
    *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
    html, body {
      background: #0E131F;
      font-family: var(--fa);
      color: var(--text-main);
      min-height: 100vh;
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 55px 24px 80px;
    }
    
    /* Top bar */
    .nav {
      position: fixed;
      top: 0; left: 0; right: 0;
      height: 48px;
      background: #090D15;
      border-bottom: 1px solid #1E2840;
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 0 28px;
      z-index: 1000;
      font-family: var(--fm);
      font-size: 11px;
      color: #8393B0;
    }
    .nav-brand {
      color: var(--copper2);
      font-weight: 700;
      letter-spacing: .16em;
      display: flex;
      align-items: center;
      gap: 9px;
      font-size: 13px;
    }
    .nav-diamond {
      width: 8px; height: 8px;
      background: var(--copper);
      transform: rotate(45deg);
    }
    .nav-dots { display: flex; gap: 5px; align-items: center; }
    .dot {
      display: inline-block;
      width: 18px; height: 4px;
      background: #1C263A;
      text-decoration: none;
      transition: all .2s ease;
      border-radius: 1px;
    }
    .dot:hover { background: var(--copper2); height: 6px; }
    .dot.active { background: var(--copper); }
    .nav-counter { color: #5B6D8E; font-weight: 600; letter-spacing: 0.08em; font-family: var(--fm); }

    /* Slides container */
    .slides {
      margin-top: 24px;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 36px;
      width: 100%;
    }
    .slide {
      width: 1120px;
      min-height: 700px;
      background: var(--warm);
      position: relative;
      overflow: hidden;
      border-radius: 4px;
      box-shadow: 0 10px 45px rgba(0,0,0,0.65);
      display: flex;
      flex-direction: column;
      border: 1px solid rgba(232, 223, 208, 0.4);
    }

    /* Top accent line */
    .tl {
      height: 4px;
      background: linear-gradient(90deg, var(--copper) 0%, var(--copper2) 30%, var(--navy) 80%, transparent 100%);
      flex-shrink: 0;
    }

    /* Watermark / Brand corner */
    .brand {
      position: absolute;
      bottom: 22px;
      left: 54px;
      display: flex;
      align-items: center;
      gap: 8px;
      font-family: var(--fm);
      font-size: 9px;
      font-weight: 700;
      letter-spacing: .22em;
      color: var(--navy2);
      opacity: .45;
      z-index: 10;
    }
    .brand-line { width: 16px; height: 1px; background: var(--copper); }
    .brand-arr {
      width: 0; height: 0;
      border-top: 3.5px solid transparent;
      border-bottom: 3.5px solid transparent;
      border-left: 6px solid var(--copper);
    }
    .pn {
      position: absolute;
      bottom: 18px;
      right: 54px;
      text-align: right;
      z-index: 10;
      font-family: var(--fm);
    }
    .pn .n {
      display: block;
      font-size: 26px;
      font-weight: 700;
      color: var(--navy);
      line-height: 1;
    }
    .pn .l {
      display: block;
      font-size: 8px;
      font-weight: 600;
      letter-spacing: .25em;
      color: var(--copper);
      margin-top: 3px;
      text-transform: uppercase;
    }

    /* Slide Content */
    .sc {
      flex: 1;
      padding: 42px 54px 58px;
      position: relative;
      z-index: 1;
    }
    .slbl {
      font-family: var(--fm);
      font-size: 9.5px;
      letter-spacing: .24em;
      text-transform: uppercase;
      color: var(--copper);
      margin-bottom: 4px;
      font-weight: 600;
    }
    .stitle {
      font-size: 29px;
      font-weight: 700;
      color: var(--navy);
      line-height: 1.25;
      margin-bottom: 4px;
    }
    .ssub {
      font-family: var(--fm);
      font-size: 11px;
      color: var(--copper);
      letter-spacing: .15em;
      text-transform: uppercase;
      margin-bottom: 22px;
      font-weight: 600;
    }
    
    h2.h {
      font-size: 15.5px;
      font-weight: 700;
      color: var(--navy);
      margin: 20px 0 10px;
      padding-bottom: 5px;
      border-bottom: 1.5px solid var(--beige);
      display: flex;
      align-items: center;
      gap: 8px;
    }
    h2.h::before {
      content: "";
      display: inline-block;
      width: 5px;
      height: 5px;
      background: var(--copper);
      transform: rotate(45deg);
    }
    p {
      font-size: 13.5px;
      line-height: 1.85;
      color: var(--text-main);
      margin-bottom: 10px;
    }
    strong { color: var(--navy); font-weight: 700; }
    code {
      font-family: var(--fm);
      font-size: 11.5px;
      background: rgba(21, 34, 56, 0.08);
      padding: 1px 6px;
      border-radius: 2px;
      color: var(--navy);
      border: 1px solid rgba(21, 34, 56, 0.12);
    }

    /* Grid Layouts */
    .grid-2 { display: grid; grid-template-columns: 1fr 1fr; gap: 24px; margin: 12px 0; }
    .grid-3 { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 16px; margin: 12px 0; }

    /* Tables */
    .dt {
      width: 100%;
      border-collapse: collapse;
      font-size: 12px;
      margin: 10px 0;
      background: var(--card-bg);
      border: 1px solid var(--beige);
    }
    .dt th {
      background: var(--navy);
      color: var(--cream);
      padding: 8px 14px;
      text-align: right;
      font-weight: 600;
      font-size: 11px;
      letter-spacing: 0.04em;
    }
    .dt td {
      padding: 8px 14px;
      border-bottom: 1px solid var(--beige);
      color: var(--text-main);
      vertical-align: middle;
      line-height: 1.6;
    }
    .dt tr:nth-child(even) td { background: rgba(244, 239, 235, 0.45); }
    .dt tr:hover td { background: rgba(200, 115, 42, 0.06); }
    .dt code { font-size: 11px; }

    /* Code blocks with RTL support for Arabic code */
    .cb {
      background: #121824;
      border-radius: 3px;
      overflow: hidden;
      margin: 12px 0;
      border-right: 3px solid var(--copper);
      box-shadow: 0 4px 12px rgba(0,0,0,0.15);
    }
    .cbh {
      background: #0A0E17;
      padding: 6px 14px;
      font-family: var(--fm);
      font-size: 9.5px;
      letter-spacing: .15em;
      color: #8C9BB5;
      text-transform: uppercase;
      display: flex;
      align-items: center;
      gap: 8px;
      border-bottom: 1px solid #1A2436;
    }
    .cbh::before {
      content: '';
      display: inline-block;
      width: 5px; height: 5px;
      border: 1px solid var(--copper);
      transform: rotate(45deg);
      flex-shrink: 0;
    }
    .cb pre {
      padding: 13px 18px;
      font-family: var(--fm);
      font-size: 12.5px;
      line-height: 1.85;
      color: #E2DDD5;
      overflow-x: auto;
      white-space: pre;
    }
    /* Arabic code: RTL by default */
    .cb.bayan-code pre,
    .cb.rtl-code pre {
      direction: rtl;
      text-align: right;
      font-family: var(--fm), var(--fa);
    }
    /* Non-Arabic code: LTR */
    .cb.ltr-code pre {
      direction: ltr;
      text-align: left;
    }

    .kw { color: #E88338; font-weight: 600; }
    .id { color: #9ABFE0; }
    .nm { color: #A6E22E; }
    .st { color: #E6DB74; }
    .cm { color: #6C7D93; font-style: italic; }
    .op { color: #F8F8F2; }
    .pn2 { color: #A0B2C6; }
    .er { color: #FF6B6B; font-weight: 600; }
    .ok { color: #2B8A3E; font-weight: 600; }

    /* Callouts & cards */
    .callout {
      border-right: 3px solid var(--copper);
      background: rgba(200, 115, 42, 0.08);
      padding: 12px 16px;
      margin: 12px 0;
      font-size: 13px;
      color: #382A1B;
      line-height: 1.75;
      border-radius: 0 3px 3px 0;
    }
    .callout strong { color: var(--copper); }
    
    .card {
      background: var(--card-bg);
      border: 1px solid var(--beige);
      padding: 14px 18px;
      border-radius: 2px;
      margin: 8px 0;
    }
    .card-title {
      font-family: var(--fm);
      font-size: 9.5px;
      letter-spacing: .16em;
      color: var(--copper);
      text-transform: uppercase;
      margin-bottom: 6px;
      font-weight: 600;
    }

    /* List bullets */
    .bl { list-style: none; margin: 8px 0; padding: 0; }
    .bl li {
      position: relative;
      padding-right: 18px;
      font-size: 13px;
      line-height: 1.8;
      color: var(--text-main);
      margin-bottom: 5px;
    }
    .bl li::before {
      content: '';
      position: absolute;
      right: 0; top: 9px;
      width: 5px; height: 5px;
      border: 1.5px solid var(--copper);
      transform: rotate(45deg);
    }

    /* Pipeline / Flow Architecture */
    .flow-chain {
      display: flex;
      flex-direction: column;
      gap: 6px;
      margin: 12px 0;
    }
    .flow-step {
      display: flex;
      align-items: center;
      background: #FFFFFF;
      border: 1px solid var(--beige);
      padding: 8px 14px;
      border-radius: 3px;
    }
    .flow-num {
      font-family: var(--fm);
      font-size: 11px;
      font-weight: 700;
      color: var(--copper);
      background: rgba(200,115,42,0.1);
      padding: 3px 7px;
      border-radius: 2px;
      margin-left: 12px;
    }
    .flow-name { font-size: 13px; font-weight: 700; color: var(--navy); min-width: 170px; }
    .flow-sub { font-family: var(--fm); font-size: 10px; color: #7B8BA6; margin-right: 10px; min-width: 140px; }
    .flow-desc { font-size: 12px; color: var(--text-muted); flex: 1; text-align: left; direction: ltr; font-family: var(--fm); }
    .flow-arr {
      display: flex;
      justify-content: center;
      color: var(--copper);
      font-size: 12px;
      margin: -2px 0;
    }

    /* ===================================================
       COVER SLIDE SPECIFIC STYLES
       =================================================== */
    .cover {
      background: radial-gradient(circle at 85% 15%, #1C2C4E 0%, #111A2E 60%, #0A101D 100%);
      color: #FFFFFF;
    }
    .cover .tl { background: linear-gradient(90deg, var(--copper) 0%, var(--copper2) 40%, transparent 100%); }
    .cover .brand { color: #A0B2D4; opacity: .7; }
    .cover .pn .n { color: #FFFFFF; }
    
    .cover-layout {
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      height: 100%;
      min-height: 630px;
      padding: 36px 52px;
      position: relative;
      z-index: 2;
    }

    /* Top Academic Header with Logo */
    .academic-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      border-bottom: 1px solid rgba(255, 255, 255, 0.12);
      padding-bottom: 18px;
    }
    .academic-dept {
      display: flex;
      flex-direction: column;
      gap: 3px;
    }
    .univ-title {
      font-size: 20px;
      font-weight: 700;
      color: #FFFFFF;
      letter-spacing: -0.01em;
    }
    .faculty-title {
      font-size: 13.5px;
      font-weight: 600;
      color: var(--copper2);
    }
    .course-title {
      font-size: 12px;
      color: rgba(255, 255, 255, 0.7);
      font-family: var(--fm);
    }
    .univ-logo-wrap {
      background: #FFFFFF;
      padding: 6px;
      border-radius: 8px;
      box-shadow: 0 4px 18px rgba(0,0,0,0.35);
      border: 2px solid var(--copper);
      display: flex;
      align-items: center;
      justify-content: center;
      width: 78px;
      height: 78px;
    }
    .univ-logo-img {
      max-width: 100%;
      max-height: 100%;
      object-fit: contain;
    }

    /* Center Hero */
    .cover-hero {
      margin: 18px 0;
    }
    .cover-badge {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      background: rgba(200, 115, 42, 0.15);
      border: 1px solid rgba(200, 115, 42, 0.35);
      padding: 4px 12px;
      border-radius: 2px;
      color: var(--copper2);
      font-family: var(--fm);
      font-size: 9.5px;
      letter-spacing: .2em;
      text-transform: uppercase;
      font-weight: 600;
      margin-bottom: 10px;
    }
    .cover-title {
      font-size: 46px;
      font-weight: 700;
      color: #FFFFFF;
      line-height: 1.15;
      margin-bottom: 6px;
      letter-spacing: -0.02em;
    }
    .cover-sub {
      font-family: var(--fm);
      font-size: 14px;
      color: var(--copper2);
      letter-spacing: .08em;
      margin-bottom: 22px;
    }

    /* Cover Meta Grid: Supervisor & Team */
    .cover-meta-grid {
      display: grid;
      grid-template-columns: 1.1fr 1.6fr;
      gap: 22px;
      background: rgba(10, 16, 29, 0.65);
      border: 1px solid rgba(255, 255, 255, 0.1);
      padding: 18px 22px;
      border-radius: 4px;
      backdrop-filter: blur(8px);
    }
    .meta-box-title {
      font-family: var(--fm);
      font-size: 9.5px;
      letter-spacing: .18em;
      color: var(--copper);
      text-transform: uppercase;
      font-weight: 700;
      margin-bottom: 8px;
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .meta-box-title::before {
      content: '';
      display: inline-block;
      width: 5px; height: 5px;
      background: var(--copper);
      transform: rotate(45deg);
    }
    .supervisor-name {
      font-size: 16px;
      font-weight: 700;
      color: #FFFFFF;
      margin-bottom: 4px;
    }
    .supervisor-role {
      font-size: 12px;
      color: rgba(255, 255, 255, 0.65);
    }
    
    .team-list {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 6px 14px;
    }
    .team-member {
      display: flex;
      align-items: center;
      gap: 7px;
      font-size: 13px;
      color: rgba(255, 255, 255, 0.9);
      font-weight: 500;
      background: rgba(255, 255, 255, 0.04);
      padding: 5px 9px;
      border-radius: 2px;
      border-right: 2px solid var(--copper);
    }

    /* Cover Footer */
    .cover-footer {
      display: flex;
      align-items: center;
      justify-content: space-between;
      border-top: 1px solid rgba(255,255,255,0.1);
      padding-top: 14px;
      margin-top: 14px;
    }
    .cover-pipeline-hint {
      display: flex;
      align-items: center;
      gap: 7px;
      font-family: var(--fm);
      font-size: 10px;
      color: rgba(255,255,255,0.7);
    }
    .cph-tag {
      background: var(--copper);
      color: #fff;
      padding: 2px 6px;
      border-radius: 2px;
      font-size: 8.5px;
      font-weight: 700;
      letter-spacing: .15em;
    }
    .cover-rights {
      font-family: var(--fm);
      font-size: 9.5px;
      letter-spacing: .18em;
      color: rgba(255,255,255,0.4);
    }

    @media print {
      body { background: white; padding: 0; }
      .nav { display: none; }
      .slides { gap: 0; margin-top: 0; }
      .slide { box-shadow: none; page-break-after: always; min-height: 100vh; border: none; }
    }
    @media (max-width: 1180px) {
      .slide { width: 100%; max-width: 1120px; }
    }
  </style>
</head>
<body>
<nav class="nav">
  <div class="nav-brand"><div class="nav-diamond"></div>BAYAN COMPILER</div>
  <div class="nav-dots">
""" + "".join([f'    <a class="dot" href="#s{i}" title="الشريحة {i}"></a>\n' for i in range(1, 16)]) + """  </div>
  <div class="nav-counter">15 SLIDES &bull; COMPILER CONSTRUCTION</div>
</nav>
<div class="slides">
"""

# ==================== SLIDE 1: COVER ====================
s1 = f"""
  <!-- SLIDE 1: COVER -->
  <section class="slide cover" id="s1">
    <div class="tl"></div>
    <div class="cover-layout">
      <!-- University Header -->
      <div class="academic-header">
        <div class="academic-dept">
          <div class="univ-title">جامعة إب &bull; Ibb University</div>
          <div class="faculty-title">كلية الحاسبات والعلوم التطبيقية</div>
          <div class="course-title">قسم تقنية المعلومات &bull; المستوى الرابع &bull; مقرر بناء المترجمات (Compiler Construction)</div>
        </div>
        <div class="univ-logo-wrap">
          <img class="univ-logo-img" src="{logo_src}" alt="شعار جامعة إب" />
        </div>
      </div>

      <!-- Main Project Hero -->
      <div class="cover-hero">
        <div class="cover-badge">
          <span>●</span> COMPILER PROJECT <span>●</span> C# 12 / .NET 8
        </div>
        <h1 class="cover-title">مُتَرجِم لُغة «بَيَان»</h1>
        <div class="cover-sub">Bayan Compiler &amp; Visual Interactive IDE</div>
        
        <!-- Supervisor & Team Grid -->
        <div class="cover-meta-grid">
          <div>
            <div class="meta-box-title">إشراف الدكتور الفاضل</div>
            <div class="supervisor-name">د. خالد الكحسة</div>
            <div class="supervisor-role">أستاذ مقرر بناء المترجمات &bull; كلية الحاسبات</div>
          </div>
          <div>
            <div class="meta-box-title">إعداد طلاب المستوى الرابع (فريق العمل)</div>
            <div class="team-list">
              <div class="team-member">اسامة شميس</div>
              <div class="team-member">محمد الادريسي</div>
              <div class="team-member">اسامة العقاب</div>
              <div class="team-member">محمد الدعيس</div>
              <div class="team-member" style="grid-column: span 2;">شغيب جازم</div>
            </div>
          </div>
        </div>
      </div>

      <!-- Footer Info -->
      <div class="cover-footer">
        <div class="cover-pipeline-hint">
          <span class="cph-tag">PIPELINE</span>
          <span>المصدر (.bayan)</span> ➔
          <span>Lexer</span> ➔
          <span>LL(1) Parser</span> ➔
          <span>Semantics</span> ➔
          <span>TAC</span> ➔
          <span>MIPS / CIL</span> ➔
          <span>output.exe</span>
        </div>
        <div class="cover-rights">IBB UNIVERSITY &bull; BAYAN COMPILER &bull; 2026</div>
      </div>
    </div>
    <div class="brand"><div class="brand-line"></div>BAYAN COMPILER<div class="brand-arr"></div></div>
    <div class="pn"><span class="n">01</span><span class="l">COVER</span></div>
  </section>
"""
slides_html.append(s1)

# ==================== SLIDE 2: OVERVIEW ====================
s2 = """
  <!-- SLIDE 2: PROJECT OVERVIEW -->
  <section class="slide" id="s2">
    <div class="tl"></div>
    <div class="sc">
      <div class="slbl">PROJECT OVERVIEW &bull; الشريحة 02</div>
      <h2 class="stitle">فكرة المشروع وأهدافه المحورية</h2>
      <div class="ssub">بناء مترجم تعليمي متكامل للغة برمجة عربية خالصة</div>

      <div class="grid-2">
        <div>
          <h2 class="h">ما الذي بنيناه؟</h2>
          <p>بنينا <strong>مترجمًا تعليميًا متكاملاً</strong> للغة برمجة عربية خالصة اسمها <strong>«بيان»</strong>.</p>
          <p>الفكرة المحورية هي تمكين المبرمج من كتابة خوارزمياته <strong>باللغة العربية تمامًا</strong> — بكلمات مفتاحية فصيحة، أسماء متغيرات ودوال عربية، ورسائل أخطاء تشخيصية واضحة ودقيقة بالعربية.</p>
          
          <div class="callout">
            <strong>لماذا هذا المشروع مهم؟</strong><br>
            المترجم هو الجسر بين لغة يفهمها الإنسان ولغة تفهمها الآلة. بناء مترجم حقيقي يعني تطبيق كامل نظريات علم الحاسب (اللغات الصورية، الأتمتة، جداول الرموز، والتمثيل الوسيط) في نظام متين.
          </div>
        </div>

        <div>
          <h2 class="h">ماذا يستطيع المترجم فعله؟</h2>
          <ul class="bl">
            <li><strong>قبول برامج مكتوبة بالعربية:</strong> دعم كامل للأبجدية والهمزات وصيغ التراكيب العربية.</li>
            <li><strong>فحص دقيق متعدد المراحل:</strong> كشف الأخطاء المعجمية والنحوية والدلالية بذكاء.</li>
            <li><strong>توليد ملف تنفيذي مباشر:</strong> إنتاج <code>output.exe</code> حقيقي يعمل فورًا على Windows.</li>
            <li><strong>توليد لغة التجميع MIPS:</strong> إنتاج <code>output.asm</code> متوافق مع محاكي MARS التعليمي.</li>
            <li><strong>معمارية منفصلة ونظيفة:</strong> فصل تام بين نواة المترجم وبيئة التطوير الرسومية.</li>
          </ul>

          <h2 class="h">المشروع = مترجم + بيئة تطوير</h2>
          <table class="dt">
            <thead>
              <tr><th>المكون البرمجي</th><th>المسؤولية والدور</th></tr>
            </thead>
            <tbody>
              <tr><td><strong>Bayan.Compiler.Core</strong></td><td>النواة التي تُجري كل مراحل الترجمة الفعلية (مكتبة C# مستقلة).</td></tr>
              <tr><td><strong>Bayan.IDE</strong></td><td>واجهة رسومية تفاعلية بكامل أدوات المحرر ومفتش المراحل.</td></tr>
              <tr><td><strong>Bayan.CLI</strong></td><td>أداة سطر أوامر للترجمة التلقائية وتشغيل الـ CI/CD والاختبارات.</td></tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
    <div class="brand"><div class="brand-line"></div>BAYAN COMPILER<div class="brand-arr"></div></div>
    <div class="pn"><span class="n">02</span><span class="l">OVERVIEW</span></div>
  </section>
"""
slides_html.append(s2)

# ==================== SLIDE 3: LANGUAGE SPEC ====================
s3 = """
  <!-- SLIDE 3: LANGUAGE SPECIFICATION -->
  <section class="slide" id="s3">
    <div class="tl"></div>
    <div class="sc">
      <div class="slbl">LANGUAGE SPECIFICATION &bull; الشريحة 03</div>
      <h2 class="stitle">مواصفات لغة «بيان» ونظام الأنواع</h2>
      <div class="ssub">لغة إجرائية عربية ذات نظام تدقيق صارم للأنواع (Statically Typed)</div>

      <div class="grid-2">
        <div>
          <h2 class="h">الأنواع المدعومة في اللغة</h2>
          <table class="dt">
            <thead>
              <tr><th>النوع العربي</th><th>المقابل الإنجليزي</th><th>أمثلة وتوضيح</th></tr>
            </thead>
            <tbody>
              <tr><td><code>صحيح</code></td><td>Integer (32-bit)</td><td><code>5</code>, <code>100</code>, <code>-3</code></td></tr>
              <tr><td><code>حقيقي</code></td><td>Real / Float (64-bit)</td><td><code>3.14</code>, <code>0.5</code>, <code>7.0</code></td></tr>
              <tr><td><code>منطقي</code></td><td>Boolean</td><td><code>صح</code>, <code>خطأ</code></td></tr>
              <tr><td><code>حرفي</code></td><td>Character (Unicode)</td><td><code>'أ'</code>, <code>'س'</code>, <code>'x'</code></td></tr>
              <tr><td><code>خيط رمزي(n)</code></td><td>Fixed-length String</td><td><code>"مرحبا بكم في بيان"</code></td></tr>
              <tr><td><code>قائمة [n]</code></td><td>Fixed-size Array</td><td>مصفوفات ثابتة الأبعاد</td></tr>
              <tr><td><code>سجل</code></td><td>Record / Struct</td><td>أنواع مركبة مخصصة بحقول متعددة</td></tr>
            </tbody>
          </table>

          <h2 class="h">أبرز ميزات اللغة التعبيرية</h2>
          <ul class="bl">
            <li><strong>الثوابت والمتغيرات:</strong> دعم <code>ثابت</code> غير القابل للتعديل و<code>متغير</code>.</li>
            <li><strong>الإجراءات وتمرير المعاملات:</strong> بالقيمة (نسخة) أو بالمرجع (تعديل مباشر).</li>
            <li><strong>حلقات التكرار الثلاث:</strong> <code>طالما</code>، <code>كرر...إلى...أضف</code>، و<code>أعد...حتى</code>.</li>
            <li><strong>مرونة الهمزات العربية:</strong> تسامح ذكي مع الهمزات (مثل <code>اقرأ</code> أو <code>اقرا</code>).</li>
          </ul>
        </div>

        <div>
          <h2 class="h">مثال كود حقيقي بلغة «بيان» (مكتوب من اليمين)</h2>
          <div class="cb bayan-code">
            <div class="cbh">grade_system.bayan</div>
            <pre><span class="kw">برنامج</span> <span class="id">نظام_تقديرات</span>;

<span class="kw">ثابت</span> <span class="id">درجة_النجاح</span> = <span class="nm">50</span>;
<span class="kw">متغير</span> <span class="id">درجة_الطالب</span> : <span class="kw">صحيح</span>;

{
    <span class="id">درجة_الطالب</span> = <span class="nm">85</span>;

    <span class="kw">إذا</span> (<span class="id">درجة_الطالب</span> &gt;= <span class="id">درجة_النجاح</span>) <span class="kw">فإن</span> {
        <span class="id">اطبع</span>(<span class="st">"الطالب ناجح بتفوق!"</span>);
    } <span class="kw">وإلا</span> {
        <span class="id">اطبع</span>(<span class="st">"لم يحقق درجة النجاح المطلوبة."</span>);
    }
}.</pre>
          </div>
          <div class="callout">
            <strong>قواعد نحوية دقيقة:</strong> ينتهي البرنامج دائمًا بنقطة <code>}.</code>، وتُكتب الأسطر البرمجية العربية من اليمين لليسان مع الحفاظ على الأقواس المنطقية <code>{ }</code> بدقة واضحة للـ Parser.
          </div>
        </div>
      </div>
    </div>
    <div class="brand"><div class="brand-line"></div>BAYAN COMPILER<div class="brand-arr"></div></div>
    <div class="pn"><span class="n">03</span><span class="l">LANGUAGE</span></div>
  </section>
"""
slides_html.append(s3)

# ==================== SLIDE 4: ARCHITECTURE ====================
s4 = """
  <!-- SLIDE 4: ARCHITECTURE -->
  <section class="slide" id="s4">
    <div class="tl"></div>
    <div class="sc">
      <div class="slbl">COMPILER ARCHITECTURE &bull; الشريحة 04</div>
      <h2 class="stitle">معمارية المترجم وخط الإنتاج (Pipeline)</h2>
      <div class="ssub">خط أنابيب متكامل من كود المصدر الخام حتى الكود الآلي القابل للتشغيل</div>

      <div class="grid-2">
        <div>
          <h2 class="h">كيف يعمل المترجم؟ (نظرة شاملة)</h2>
          <p>المترجم يُحوّل كود المصدر المكتوب بلغة «بيان» إلى ملف تنفيذي عبر <strong>سلسلة من المراحل المتتابعة</strong>؛ كل مرحلة تتسلم مدخلات محددة من سابقتها، تُجري عمليات الفحص والتحويل، وتُسلّم مخرجات منقحة للمرحلة التالية.</p>

          <div class="callout">
            <strong>مبدأ العزل والفصل الصارم:</strong><br>
            إذا اكتُشف أي خطأ (معجمي، نحوي، أو دلالي) في أي مرحلة، <strong>يتوقف خط الإنتاج فورًا</strong> ويتم جمع التشخيصات دون استمرار غير آمن نحو توليد الكود.
          </div>

          <table class="dt">
            <thead>
              <tr><th>المرحلة</th><th>المدخلات</th><th>المخرجات</th></tr>
            </thead>
            <tbody>
              <tr><td><strong>01. Lexer</strong></td><td>نص كود .bayan</td><td>قائمة الرموز (Tokens)</td></tr>
              <tr><td><strong>02. Parser</strong></td><td>قائمة الـ Tokens</td><td>شجرة الإعراب (AST)</td></tr>
              <tr><td><strong>03. Semantic</strong></td><td>شجرة AST الخام</td><td>AST محققة + جدول الرموز</td></tr>
              <tr><td><strong>04. IR Gen</strong></td><td>شجرة AST المحققة</td><td>كود العناوين الثلاثية (TAC)</td></tr>
              <tr><td><strong>05. CodeGen</strong></td><td>برنامج IrProgram</td><td>MIPS (.asm) و CIL (.il)</td></tr>
              <tr><td><strong>06. Assembler</strong></td><td>كود CIL وسيط</td><td>ملف تنفيذي مباشر (.exe)</td></tr>
            </tbody>
          </table>
        </div>

        <div>
          <h2 class="h">مخطط خط الإنتاج المتتابع (Visual Flow)</h2>
          <div class="flow-chain">
            <div class="flow-step">
              <span class="flow-num">00</span>
              <span class="flow-name">كود المصدر العربي</span>
              <span class="flow-sub">Source Code</span>
              <span class="flow-desc">input.bayan (UTF-8)</span>
            </div>
            <div class="flow-arr">▼</div>
            <div class="flow-step">
              <span class="flow-num">01</span>
              <span class="flow-name">التحليل المعجمي</span>
              <span class="flow-sub">Lexical Analysis</span>
              <span class="flow-desc">IReadOnlyList&lt;Token&gt;</span>
            </div>
            <div class="flow-arr">▼</div>
            <div class="flow-step">
              <span class="flow-num">02</span>
              <span class="flow-name">التحليل النحوي LL(1)</span>
              <span class="flow-sub">Syntax Analysis</span>
              <span class="flow-desc">ProgramNode (AST Root)</span>
            </div>
            <div class="flow-arr">▼</div>
            <div class="flow-step">
              <span class="flow-num">03</span>
              <span class="flow-name">التحليل الدلالي</span>
              <span class="flow-sub">Semantic Analysis</span>
              <span class="flow-desc">SymbolTable + Typed AST</span>
            </div>
            <div class="flow-arr">▼</div>
            <div class="flow-step">
              <span class="flow-num">04</span>
              <span class="flow-name">توليد الكود الوسيط</span>
              <span class="flow-sub">Three-Address Code</span>
              <span class="flow-desc">IrProgram (TAC Instructions)</span>
            </div>
            <div class="flow-arr">▼</div>
            <div class="flow-step" style="border: 1.5px solid var(--copper);">
              <span class="flow-num" style="background:var(--copper); color:#fff;">05</span>
              <span class="flow-name" style="color:var(--copper);">مسار التوليد المزدوج</span>
              <span class="flow-sub">Code Generation</span>
              <span class="flow-desc">MIPS (MARS) &bull; CIL (ILAsm ➔ EXE)</span>
            </div>
          </div>
        </div>
      </div>
    </div>
    <div class="brand"><div class="brand-line"></div>BAYAN COMPILER<div class="brand-arr"></div></div>
    <div class="pn"><span class="n">04</span><span class="l">ARCHITECTURE</span></div>
  </section>
"""
slides_html.append(s4)

# ==================== SLIDE 5: LEXICAL ====================
s5 = """
  <!-- SLIDE 5: LEXICAL ANALYSIS -->
  <section class="slide" id="s5">
    <div class="tl"></div>
    <div class="sc">
      <div class="slbl">LEXICAL ANALYSIS &bull; الشريحة 05</div>
      <h2 class="stitle">التحليل المعجمي واستخراج الوحدات (Tokens)</h2>
      <div class="ssub">المرحلة الأولى: تحويل سيل الحروف الخام إلى وحدات معنوية واضحة المعالم</div>

      <div class="grid-2">
        <div>
          <h2 class="h">ما هو التحليل المعجمي؟</h2>
          <p>هو أولى مراحل المترجم، حيث يقرأ النص حرفًا بحرف ويجمعه في وحدات منطقية تُسمى <strong>Tokens</strong> متجاهلاً الفراغات والتعليقات، مع تسجيل موقع كل رمز في المصدر بدقة (السطر والعمود).</p>

          <h2 class="h">مكونات الـ Token في لغة بيان</h2>
          <table class="dt">
            <thead>
              <tr><th>المكون</th><th>المعنى البرمجي</th><th>مثال حي</th></tr>
            </thead>
            <tbody>
              <tr><td><strong>Type</strong></td><td>تصنيف الوحدة المعجمية</td><td><code>Const</code>, <code>Identifier</code>, <code>IntLit</code></td></tr>
              <tr><td><strong>Lexeme</strong></td><td>النص الأصلي من الكود</td><td><code>ثابت</code>, <code>درجة_النجاح</code>, <code>50</code></td></tr>
              <tr><td><strong>Span</strong></td><td>موقع البداية والنهاية</td><td><code>سطر 1، عمود 7</code></td></tr>
            </tbody>
          </table>

          <div class="callout">
            <strong>إحصائية المحلل المعجمي:</strong><br>
            يتعرف المحلل في مشروعنا على <strong>94 نوعًا مختلفًا من الـ Tokens</strong> تغطي الكلمات المفتاحية، المعاملات الحسابية والمنطقية، علامات الترقيم، والأعداد والنصوص.
          </div>
        </div>

        <div>
          <h2 class="h">مثال عملي: تفكيك جملة إلى Tokens</h2>
          <div class="cb bayan-code">
            <div class="cbh">مصدر الكود الأصلي (عربي من اليمين)</div>
            <pre><span class="kw">ثابت</span> <span class="id">درجة_النجاح</span> = <span class="nm">50</span>;</pre>
          </div>

          <table class="dt">
            <thead>
              <tr><th>نوع الرمز (TokenType)</th><th>النص (Lexeme)</th><th>الموقع (Span)</th></tr>
            </thead>
            <tbody>
              <tr><td><code>ConstKeyword</code></td><td><code>ثابت</code></td><td>Line 1, Col 1</td></tr>
              <tr><td><code>Identifier</code></td><td><code>درجة_النجاح</code></td><td>Line 1, Col 6</td></tr>
              <tr><td><code>Equal</code></td><td><code>=</code></td><td>Line 1, Col 18</td></tr>
              <tr><td><code>IntegerLiteral</code></td><td><code>50</code></td><td>Line 1, Col 20</td></tr>
              <tr><td><code>Semicolon</code></td><td><code>;</code></td><td>Line 1, Col 22</td></tr>
            </tbody>
          </table>

          <h2 class="h">معالجة الحالات الخاصة</h2>
          <ul class="bl">
            <li><strong>تطبيع الهمزات:</strong> دعم الكلمات بصورها المتعددة (<code>اقرأ</code> و <code>اقرا</code>).</li>
            <li><strong>الرموز المركبة:</strong> فحص متعدد المحارف للمقارنات (<code>&gt;=</code>, <code>==</code>, <code>!=</code>).</li>
          </ul>
        </div>
      </div>
    </div>
    <div class="brand"><div class="brand-line"></div>BAYAN COMPILER<div class="brand-arr"></div></div>
    <div class="pn"><span class="n">05</span><span class="l">LEXICAL</span></div>
  </section>
"""
slides_html.append(s5)

# ==================== SLIDE 6: SYNTAX ANALYSIS ====================
s6 = """
  <!-- SLIDE 6: SYNTAX ANALYSIS -->
  <section class="slide" id="s6">
    <div class="tl"></div>
    <div class="sc">
      <div class="slbl">SYNTAX ANALYSIS &bull; الشريحة 06</div>
      <h2 class="stitle">التحليل النحوي وشجرة الإعراب المجردة (AST)</h2>
      <div class="ssub">المرحلة الثانية: التحقق من القواعد النحوية وبناء الهيكل الشجري الهرمي للبرنامج</div>

      <div class="grid-2">
        <div>
          <h2 class="h">نوع المحلل: LL(1) التنازلي التنبؤي</h2>
          <p>يعتمد المترجم على محلل نحوي تنازلي من نوع <strong>LL(1) (Recursive Descent Parser)</strong>:</p>
          <table class="dt">
            <thead>
              <tr><th>الرمز</th><th>المعنى التقني المنهجي</th></tr>
            </thead>
            <tbody>
              <tr><td><strong>L</strong> (الأول)</td><td>قراءة المدخلات من اليمين إلى اليسار (وفق سياق الرموز).</td></tr>
              <tr><td><strong>L</strong> (الثاني)</td><td>اشتقاق أيسر لأطراف قواعد النحو (Leftmost Derivation).</td></tr>
              <tr><td><strong>(1)</strong></td><td>النظر لرمز واحد مستقبلي فقط (Lookahead = 1) لاتخاذ القرار.</td></tr>
            </tbody>
          </table>

          <div class="callout">
            <strong>ما هي شجرة الـ AST؟</strong><br>
            هي تمثيل شجري مجرد لبنية البرنامج؛ يمثل الجذر البرنامج كاملاً، والفروع هي الدوال والجمل الشرطية والحلقات، بينما الأوراق هي المعرفات والقيم والعمليات المباشرة.
          </div>
        </div>

        <div>
          <h2 class="h">تمثيل شجرة AST لجملة شرطية</h2>
          <div class="cb bayan-code">
            <div class="cbh">الكود المدخل (عربي)</div>
            <pre><span class="kw">إذا</span> (<span class="id">س</span> &gt; <span class="nm">0</span>) <span class="kw">فإن</span> { <span class="id">اطبع</span>(<span class="st">"موجب"</span>); }</pre>
          </div>

          <div class="cb ltr-code">
            <div class="cbh">مخرجات فاحص الشجرة (AST Inspector)</div>
            <pre><span class="kw">IfStatementNode</span>
├── <span class="id">Condition</span>: <span class="kw">BinaryOpExprNode</span> (&gt;)
│   ├── <span class="id">Left</span>:  <span class="kw">VariableAccessNode</span> (<span class="st">"س"</span>)
│   └── <span class="id">Right</span>: <span class="kw">LiteralNode</span> (<span class="nm">0</span> : صحيح)
└── <span class="id">ThenBlock</span>: <span class="kw">BlockNode</span>
    └── <span class="kw">PrintStatementNode</span>
        └── <span class="id">Argument</span>: <span class="kw">LiteralNode</span> (<span class="st">"موجب"</span> : نص)</pre>
          </div>

          <p style="font-size:12px; color:var(--text-muted);">
            تسمح هذه الشجرة بتسهيل عمليات التحقق الدلالي وتوليد الكود الوسيط دون الحاجة للتعامل مجددًا مع النصوص أو علامات الترقيم المجردة كالفواصل المنقوطة.
          </p>
        </div>
      </div>
    </div>
    <div class="brand"><div class="brand-line"></div>BAYAN COMPILER<div class="brand-arr"></div></div>
    <div class="pn"><span class="n">06</span><span class="l">SYNTAX</span></div>
  </section>
"""
slides_html.append(s6)

# ==================== SLIDE 7: SEMANTIC ANALYSIS ====================
s7 = """
  <!-- SLIDE 7: SEMANTIC ANALYSIS -->
  <section class="slide" id="s7">
    <div class="tl"></div>
    <div class="sc">
      <div class="slbl">SEMANTIC ANALYSIS &bull; الشريحة 07</div>
      <h2 class="stitle">التحليل الدلالي وفحص المعنى والأنواع</h2>
      <div class="ssub">المرحلة الثالثة: التأكد من منطقية العمليات وتوافق الأنواع وصحة النطاقات</div>

      <div class="grid-2">
        <div>
          <h2 class="h">ما هو التحليل الدلالي ولماذا نحتاجه؟</h2>
          <p>التحليل النحوي يضمن فقط أن تسلسل الكلمات صحيح شكليًا، بينما التحليل الدلالي هو المسؤول عن <strong>فحص المعنى وصحة العلاقات المنطقية</strong> داخل الكود.</p>
          
          <div class="callout">
            <strong>مثال توضيحي للفارق:</strong><br>
            تعبير مثل: <code>س = 5 + "نص";</code> هو تعبير صحيح نحويًا (متغير = قيمة + قيمة)، لكنه <strong>خاطئ دلاليًا</strong> لأن لغة بيان تمنع جمع عدد صحيح مع قيمة نصية.
          </div>

          <h2 class="h">المسؤوليات الرئيسية للمحلل الدلالي</h2>
          <ul class="bl">
            <li><strong>فحص توافق الأنواع (Type Checking):</strong> التأكد من تطابق أنواع طرفي العمليات الحسابية والمنطقية.</li>
            <li><strong>حماية الثوابت (Immutability):</strong> منع إعادة إسناد أي قيمة جديدة للمعرفات المعرفة بـ <code>ثابت</code>.</li>
            <li><strong>التحقق من المعرفات (Scope Resolution):</strong> التحقق من تعريف المتغيرات أو الإجراءات قبل استخدامها.</li>
          </ul>
        </div>

        <div>
          <h2 class="h">جدول نماذج الفحص الدلالي</h2>
          <table class="dt">
            <thead>
              <tr><th>الحالة المختبرة</th><th>الحكم الدلالي</th><th>الإجراء المتخذ</th></tr>
            </thead>
            <tbody>
              <tr>
                <td><code>س = 10 + 20;</code></td>
                <td><span class="ok">✅ صحيح</span></td>
                <td>كلا الطرفين <code>صحيح</code>، الناتج <code>صحيح</code>.</td>
              </tr>
              <tr>
                <td><code>س = 10 + صح;</code></td>
                <td><span class="er">❌ خطأ نوع</span></td>
                <td>رفض العملية: لا يمكن جمع عدد مع قيمة منطقية.</td>
              </tr>
              <tr>
                <td><code>ثابت ط = 3.14; ط = 5;</code></td>
                <td><span class="er">❌ خطأ تعديل</span></td>
                <td>منع التعديل: لا يمكن الإسناد إلى ثابت معرّف.</td>
              </tr>
              <tr>
                <td><code>ص = ص + 1;</code> (دون تعريف)</td>
                <td><span class="er">❌ غير معرّف</span></td>
                <td>المعرف <code>ص</code> غير موجود في جدول الرموز الحالي.</td>
              </tr>
              <tr>
                <td>تمرير معامل بالمرجع لقيمة ثابتة</td>
                <td><span class="er">❌ خطأ مرجع</span></td>
                <td>التمرير بالمرجع يتطلب متغيرًا قابلاً للتعديل في الذاكرة.</td>
              </tr>
            </tbody>
          </table>

          <div class="card">
            <div class="card-title">مخرجات التحليل الدلالي</div>
            <p style="font-size:12px; margin:0;">ينتج عن هذه المرحلة شجرة AST مدققة ومزودة بكافة معلومات الأنواع، إلى جانب جدول رموز كامل وجاهز للاستخدام في مراحل توليد الكود.</p>
          </div>
        </div>
      </div>
    </div>
    <div class="brand"><div class="brand-line"></div>BAYAN COMPILER<div class="brand-arr"></div></div>
    <div class="pn"><span class="n">07</span><span class="l">SEMANTICS</span></div>
  </section>
"""
slides_html.append(s7)

# ==================== SLIDE 8: SYMBOL TABLE ====================
s8 = """
  <!-- SLIDE 8: SYMBOL TABLE -->
  <section class="slide" id="s8">
    <div class="tl"></div>
    <div class="sc">
      <div class="slbl">SYMBOL TABLE &bull; الشريحة 08</div>
      <h2 class="stitle">جدول الرموز وإدارة النطاقات الهرمية</h2>
      <div class="ssub">المخزن المركزي لذاكرة المترجم وإدارة مجالات الرؤية (Lexical Scoping)</div>

      <div class="grid-2">
        <div>
          <h2 class="h">ما هو جدول الرموز؟</h2>
          <p>جدول الرموز هو <strong>قاعدة البيانات المركزية المؤقتة للمترجم</strong> أثناء عمله. يُسجل فيه كل اسم ومعرف يظهر في البرنامج مع خصائصه ونوعه وموقعه وعمقه النطاقي.</p>

          <h2 class="h">ماذا يُخزّن لكل معرّف؟</h2>
          <table class="dt">
            <thead>
              <tr><th>الحقل</th><th>الدور والوظيفة</th><th>مثال</th></tr>
            </thead>
            <tbody>
              <tr><td><strong>الاسم (Name)</strong></td><td>الاسم المكتوب بالعربية في المصدر</td><td><code>درجة_النجاح</code></td></tr>
              <tr><td><strong>الفئة (Kind)</strong></td><td>طبيعة المعرف البرمجية</td><td>متغير، ثابت، إجراء، نوع</td></tr>
              <tr><td><strong>النوع (Type)</strong></td><td>نوع البيانات المخصص له</td><td><code>صحيح</code>، <code>حقيقي</code>، <code>منطقي</code></td></tr>
              <tr><td><strong>القراءة فقط</strong></td><td>حالة حماية الثوابت</td><td>نعم للثوابت، لا للمتغيرات</td></tr>
              <tr><td><strong>النطاق (Scope)</strong></td><td>المجال الهرمي الحالي</td><td>عام (Global) أو محلي (Local)</td></tr>
            </tbody>
          </table>
        </div>

        <div>
          <h2 class="h">النطاقات المتداخلة (Hierarchical Scopes)</h2>
          <p>يدعم المترجم بنية نطاقات شجرية متداخلة؛ بحيث يرى النطاق الداخلي المتغيرات المعرفة في النطاق الخارجي (Lexical Scoping)، بينما تُحجب المتغيرات الداخلية فور الخروج من كتلتها.</p>

          <div class="cb rtl-code">
            <div class="cbh">مخطط النطاقات الهرمية (عربي)</div>
            <pre><span class="kw">[ النطاق العام - Global Scope ]</span>
  ├── <span class="id">درجة_النجاح</span> : ثابت (صحيح = 50)
  ├── <span class="id">المعدل_العام</span> : متغير (حقيقي)
  └── <span class="kw">[ نطاق إجراء: حساب_النتيجة ]</span>
        ├── <span class="id">درجة</span> : معامل (صحيح)
        └── <span class="kw">[ نطاق كتلة شرطية محصورة ]</span>
              └── <span class="id">فرق</span> : متغير محلي (صحيح)</pre>
          </div>

          <div class="callout">
            <strong>كشف أخطاء إعادة التعريف:</strong> يمنع جدول الرموز تعريف معرفين بنفس الاسم داخل نفس النطاق، لكنه يسمح بحجب (Shadowing) المعرفات العامة في نطاقات فرعية مستقلة بأمان.
          </div>
        </div>
      </div>
    </div>
    <div class="brand"><div class="brand-line"></div>BAYAN COMPILER<div class="brand-arr"></div></div>
    <div class="pn"><span class="n">08</span><span class="l">SYMBOLS</span></div>
  </section>
"""
slides_html.append(s8)

# ==================== SLIDE 9: ERROR HANDLING ====================
s9 = """
  <!-- SLIDE 9: ERROR HANDLING -->
  <section class="slide" id="s9">
    <div class="tl"></div>
    <div class="sc">
      <div class="slbl">ERROR HANDLING &bull; الشريحة 09</div>
      <h2 class="stitle">نظام معالجة الأخطاء والتعافي الذكي (Panic Mode)</h2>
      <div class="ssub">اكتشاف شامل للأخطاء مع رسائل تشخيصية عربية واضحة ومواقع محددة بدقة</div>

      <div class="grid-2">
        <div>
          <h2 class="h">فلسفة التعامل مع الأخطاء</h2>
          <p>لا يكتفي مترجم «بيان» بالتوقف والانهيار عند أول خطأ يواجهه؛ بل يطبق استراتيجية <strong>الاسترداد والتعافي الذكي (Panic-Mode Error Recovery)</strong> عبر تخطي الرموز حتى الوصول لرمز مزامنة آمن (مثل الفاصلة المنقوطة <code>;</code> أو قوس الإغلاق <code>}</code>).</p>
          
          <p>يتيح هذا النهج اكتشاف <strong>عدة أخطاء برمجية في جلسة ترجمة واحدة</strong>، مما يسهل على المبرمج إصلاح برنامجه دفعة واحدة دون تكرار الترجمة لكل سطر.</p>

          <div class="callout">
            <strong>رسائل خطأ عربية أصيلة:</strong><br>
            صيغت جميع رسائل الأخطاء بلغة عربية فصيحة ومباشرة تشرح سبب الخطأ بدلاً من الرموز المبهمة.
          </div>
        </div>

        <div>
          <h2 class="h">أمثلة حقيقية لرسائل الأخطاء التشخيصية</h2>
          <div class="cb rtl-code">
            <div class="cbh">مخرجات مجمّع الأخطاء في بيئة التطوير (Diagnostics)</div>
            <pre><span class="er">[خطأ نحوي]</span> سطر 4، عمود 15:
  متوقع رمز ';' في نهاية التصريح لكن تم العثور على 'متغير'.

<span class="er">[خطأ دلالي]</span> سطر 9، عمود 8:
  لا يمكن إسناد قيمة للمعرف 'درجة_النجاح' لأنه معرّف كثابت.

<span class="er">[خطأ نوع]</span> سطر 12، عمود 20:
  العملية الحسابية '+' غير معرّفة بين نوع 'صحيح' ونوع 'منطقي'.

<span class="er">[خطأ نطاق]</span> سطر 18، عمود 5:
  المعرف 'الناتج_النهائي' غير معرّف في هذا النطاق.</pre>
          </div>

          <h2 class="h">رموز المزامنة المعتمدة في الـ Parser</h2>
          <div style="display:flex; gap:8px; flex-wrap:wrap; margin-top:8px;">
            <code>; (فاصلة منقوطة)</code>
            <code>} (قوس كتلة)</code>
            <code>متغير</code>
            <code>ثابت</code>
            <code>إذا</code>
            <code>طالما</code>
          </div>
        </div>
      </div>
    </div>
    <div class="brand"><div class="brand-line"></div>BAYAN COMPILER<div class="brand-arr"></div></div>
    <div class="pn"><span class="n">09</span><span class="l">ERRORS</span></div>
  </section>
"""
slides_html.append(s9)

# ==================== SLIDE 10: TAC ====================
s10 = """
  <!-- SLIDE 10: THREE-ADDRESS CODE -->
  <section class="slide" id="s10">
    <div class="tl"></div>
    <div class="sc">
      <div class="slbl">INTERMEDIATE REPRESENTATION &bull; الشريحة 10</div>
      <h2 class="stitle">الكود الوسيط وكود العناوين الثلاثية (TAC)</h2>
      <div class="ssub">المرحلة الرابعة: تبسيط التعبيرات المعقدة وتمثيل مستقل عن المنصة المستهدفة</div>

      <div class="grid-2">
        <div>
          <h2 class="h">ما هو الكود الوسيط (IR) ولماذا نستخدمه؟</h2>
          <p>الكود الوسيط هو <strong>جسر محايد</strong> بين لغة البرمجة عالية المستوى (AST) ولغات التجميع المتباينة للآلة. يساعد في عزل مراحل التحليل عن مراحل التوليد وتحسين الكود.</p>

          <table class="dt">
            <thead>
              <tr><th>مقارنة المعمارية</th><th>بدون كود وسيط</th><th>مع كود وسيط (TAC)</th></tr>
            </thead>
            <tbody>
              <tr><td><strong>التعقيد</strong></td><td>توليد التجميع مباشرة من AST معقد وصعب الصيانة.</td><td>تفكيك التعبيرات لتعليمات أولية بسيطة وموحدة.</td></tr>
              <tr><td><strong>تعدد المنصات</strong></td><td>يتطلب إعادة كتابة كاملة لكل معمارية معالج.</td><td>نفس كود الـ TAC يغذي MIPS و CIL و x86 بسهولة.</td></tr>
            </tbody>
          </table>

          <div class="callout">
            <strong>قاعدة كود العناوين الثلاثية (TAC):</strong><br>
            كل تعليمة تتكون على الأكثر من ثلاثة معاملات:<br>
            <code>النتيجة = المعامل_الأول [العملية] المعامل_الثاني</code>
          </div>
        </div>

        <div>
          <h2 class="h">مثال عملي لتحويل تعبير مركب</h2>
          <div class="cb bayan-code">
            <div class="cbh">كود بيان الأصلي (عربي من اليمين)</div>
            <pre><span class="id">النتيجة</span> = (<span class="id">س</span> + <span class="id">ص</span>) * (<span class="id">ع</span> - <span class="nm">2</span>);</pre>
          </div>

          <div class="cb ltr-code">
            <div class="cbh">الكود الوسيط الناتج (TAC Instructions)</div>
            <pre><span class="cm">// تفكيك التعبير إلى متغيرات مؤقتة t0, t1...</span>
<span class="kw">t0</span> = <span class="id">س</span> + <span class="id">ص</span>
<span class="kw">t1</span> = <span class="id">ع</span> - <span class="nm">2</span>
<span class="kw">t2</span> = <span class="kw">t0</span> * <span class="kw">t1</span>
<span class="id">النتيجة</span> = <span class="kw">t2</span></pre>
          </div>

          <h2 class="h">تعليمات القفز والتحكم في TAC</h2>
          <p style="font-size:12px; color:var(--text-muted);">
            تُترجم الجمل الشرطية وحلقات التكرار إلى تعليمات قفز مشروط وتسميات (Labels):<br>
            <code>ifFalse t0 goto L1</code> و <code>goto L2</code>
          </p>
        </div>
      </div>
    </div>
    <div class="brand"><div class="brand-line"></div>BAYAN COMPILER<div class="brand-arr"></div></div>
    <div class="pn"><span class="n">10</span><span class="l">TAC / IR</span></div>
  </section>
"""
slides_html.append(s10)

# ==================== SLIDE 11: ASSEMBLY ====================
s11 = """
  <!-- SLIDE 11: ASSEMBLY GENERATION -->
  <section class="slide" id="s11">
    <div class="tl"></div>
    <div class="sc">
      <div class="slbl">ASSEMBLY GENERATION &bull; الشريحة 11</div>
      <h2 class="stitle">توليد لغة التجميع (MIPS Assembly &amp; CIL)</h2>
      <div class="ssub">المرحلة الخامسة: التوليد المزدوج لمعمارية معالجات MIPS ولبيئة تشغيل .NET CIL</div>

      <div class="grid-2">
        <div>
          <h2 class="h">المولد الأول: MIPS Assembly (MARS)</h2>
          <p>يولد كود تجميع متكامل لمعمارية <strong>MIPS 32-bit</strong> متوافق مع محاكي MARS التعليمي الشهير لمقررات تنظيم الحاسبات:</p>
          <ul class="bl">
            <li><strong>تخصيص المسجلات:</strong> استخدام مسجلات <code>$t0-$t9</code> للمتغيرات المؤقتة، و<code>$s0-$s7</code> للقيم المحفوظة.</li>
            <li><strong>قسم البيانات <code>.data</code>:</strong> حجز مساحات النصوص والأعداد الثابتة.</li>
            <li><strong>قسم التعليمات <code>.text</code>:</strong> ترجمة العمليات الحسابية ونداءات النظام <code>syscall</code> للإدخال والطباعة.</li>
          </ul>

          <div class="cb ltr-code">
            <div class="cbh">مقتطف من output.asm الناتج</div>
            <pre><span class="kw">.text</span>
<span class="kw">main:</span>
    <span class="kw">li</span>   <span class="id">$t0</span>, <span class="nm">85</span>
    <span class="kw">li</span>   <span class="id">$t1</span>, <span class="nm">50</span>
    <span class="kw">bge</span>  <span class="id">$t0</span>, <span class="id">$t1</span>, <span class="id">L_Pass</span>
    <span class="kw">j</span>    <span class="id">L_Fail</span></pre>
          </div>
        </div>

        <div>
          <h2 class="h">المولد الثاني: CIL (Common Intermediate Language)</h2>
          <p>يولد تعليمات <strong>CIL المستندة إلى المكدس (Stack-Based)</strong> والمعتمدة في معيار ECMA-335 لبيئة تشغيل .NET:</p>
          <ul class="bl">
            <li><strong>تحميل ووضع القيم:</strong> استخدام تعليمات <code>ldc.i4</code>, <code>ldloc</code>, و <code>stloc</code>.</li>
            <li><strong>استدعاءات النظام:</strong> استخدام دوال <code>System.Console::WriteLine</code> للإخراج السريع.</li>
            <li><strong>الإنتاج المباشر:</strong> حفظ الكود في ملف <code>output.il</code> ليكون جاهزًا للتجميع الفوري.</li>
          </ul>

          <div class="cb ltr-code">
            <div class="cbh">مقتطف من output.il الناتج</div>
            <pre><span class="kw">ldc.i4.s</span> <span class="nm">85</span>
<span class="kw">stloc.0</span>
<span class="kw">ldloc.0</span>
<span class="kw">ldc.i4.s</span> <span class="nm">50</span>
<span class="kw">bge.s</span>    <span class="id">IL_PASS</span></pre>
          </div>
        </div>
      </div>
    </div>
    <div class="brand"><div class="brand-line"></div>BAYAN COMPILER<div class="brand-arr"></div></div>
    <div class="pn"><span class="n">11</span><span class="l">ASSEMBLY</span></div>
  </section>
"""
slides_html.append(s11)

# ==================== SLIDE 12: EXECUTABLE & IDE ====================
s12 = """
  <!-- SLIDE 12: EXECUTABLE & IDE -->
  <section class="slide" id="s12">
    <div class="tl"></div>
    <div class="sc">
      <div class="slbl">EXECUTABLE &amp; IDE &bull; الشريحة 12</div>
      <h2 class="stitle">إنتاج الملف التنفيذي والبيئة التفاعلية (IDE)</h2>
      <div class="ssub">المرحلة السادسة: إنتاج ملف output.exe مستقل وتشغيله عبر الطرفية التفاعلية المدمجة</div>

      <div class="grid-2">
        <div>
          <h2 class="h">مسار بناء الملف التنفيذي (EXE Pipeline)</h2>
          <div class="flow-chain" style="margin: 14px 0;">
            <div class="flow-step">
              <span class="flow-num">1</span>
              <span class="flow-name">كود TAC الوسيط</span>
              <span class="flow-sub">IrProgram</span>
              <span class="flow-desc">تمثيل محايد للعمليات</span>
            </div>
            <div class="flow-arr">▼</div>
            <div class="flow-step">
              <span class="flow-num">2</span>
              <span class="flow-name">CilGenerator</span>
              <span class="flow-sub">توليد CIL</span>
              <span class="flow-desc">إنتاج ملف output.il</span>
            </div>
            <div class="flow-arr">▼</div>
            <div class="flow-step">
              <span class="flow-num">3</span>
              <span class="flow-name">ilasm.exe Assembler</span>
              <span class="flow-sub">مجمع .NET SDK</span>
              <span class="flow-desc">تحويل الـ IL إلى PE Binary</span>
            </div>
            <div class="flow-arr">▼</div>
            <div class="flow-step" style="border: 1.5px solid var(--copper); background: #FFFDF9;">
              <span class="flow-num" style="background:var(--copper); color:#fff;">4</span>
              <span class="flow-name" style="color:var(--copper);">output.exe</span>
              <span class="flow-sub">Native Executable</span>
              <span class="flow-desc">تشغيل فوري على نظام Windows</span>
            </div>
          </div>
        </div>

        <div>
          <h2 class="h">الطرفية التفاعلية في بيئة التطوير (IDE)</h2>
          <p>تحتوي بيئة Bayan IDE على <strong>طرفية إدخال وإخراج تفاعلية حية</strong>؛ عندما يُطلب من البرنامج إدخال عبر تعليمة <code>اقرأ(س)</code>:</p>
          <ul class="bl">
            <li>يتم تشغيل <code>output.exe</code> في خلفية غير متزامنة مع توجيه الـ Standard I/O.</li>
            <li>تظهر نافذة إدخال خاصة للمستخدم داخل محرر الـ IDE.</li>
            <li>تُعاد النتائج فورًا للطرفية المدمجة مع دعم كامل للخطوط العربية.</li>
          </ul>

          <div class="callout">
            <strong>استقلالية تامة:</strong> الملف التنفيذي الناتج <code>output.exe</code> مستقل تمامًا ويمكن نسخه وتشغيله على أي حاسوب مثبت عليه .NET دون الحاجة لوجود مترجم بيان نفسه!
          </div>
        </div>
      </div>
    </div>
    <div class="brand"><div class="brand-line"></div>BAYAN COMPILER<div class="brand-arr"></div></div>
    <div class="pn"><span class="n">12</span><span class="l">EXE &amp; IDE</span></div>
  </section>
"""
slides_html.append(s12)

# ==================== SLIDE 13: PROJECT STRUCTURE ====================
s13 = """
  <!-- SLIDE 13: PROJECT STRUCTURE -->
  <section class="slide" id="s13">
    <div class="tl"></div>
    <div class="sc">
      <div class="slbl">PROJECT STRUCTURE &bull; الشريحة 13</div>
      <h2 class="stitle">بنية المشروع البرمجية والتصميم المعماري</h2>
      <div class="ssub">تنظيم هندسي متين يفصل النواة عن واجهات المستخدم وفق معايير هندسة البرمجيات</div>

      <div class="grid-2">
        <div>
          <h2 class="h">الهيكل الشجري لحل المشروع (Solution Structure)</h2>
          <div class="cb ltr-code">
            <div class="cbh">Bayan.sln Architecture</div>
            <pre><span class="kw">Bayan.sln</span>
├── <span class="id">Bayan.Compiler.Core</span>      <span class="cm">← نواة المترجم المشتركة (Class Lib)</span>
│   ├── <span class="kw">Lexing</span>               <span class="cm">← المحلل المعجمي والتوكينات</span>
│   ├── <span class="kw">Parsing</span>              <span class="cm">← محلل LL(1) التنازلي</span>
│   ├── <span class="kw">Syntax</span>               <span class="cm">← عقد شجرة AST</span>
│   ├── <span class="kw">Semantics</span>            <span class="cm">← التحليل الدلالي وجدول الرموز</span>
│   ├── <span class="kw">Intermediate</span>         <span class="cm">← التمثيل الوسيط TAC</span>
│   ├── <span class="kw">CodeGeneration</span>       <span class="cm">← مولدات MIPS و CIL و EXE</span>
│   └── <span class="kw">Diagnostics</span>          <span class="cm">← إدارة ونقل رسائل الأخطاء</span>
├── <span class="id">Bayan.IDE</span>                <span class="cm">← بيئة التطوير الرسومية (WinForms)</span>
└── <span class="id">Bayan.CLI</span>                <span class="cm">← واجهة سطر الأوامر المستقلة</span></pre>
          </div>
        </div>

        <div>
          <h2 class="h">مبادئ التصميم المطبقة في البناء</h2>
          <ul class="bl">
            <li><strong>مبدأ المسؤولية المفردة (SRP):</strong> كل مجلد مسؤول عن مرحلة واحدة فقط من مراحل المترجم.</li>
            <li><strong>فصل الاهتمامات (Separation of Concerns):</strong> النواة <code>Compiler.Core</code> لا تحتوي على أي كود واجهات رسومية، مما يتيح تشغيلها في السحابة أو الـ CLI أو الـ IDE بنفس الكفاءة.</li>
            <li><strong>عدم الاعتمادية الدائرية (No Circular Dependencies):</strong> تدفق البيانات يسير في اتجاه واحد من التحليل المعجمي نحو توليد الكود.</li>
            <li><strong>سهولة التوسعة (Extensibility):</strong> إمكانية إضافة مولدات كود جديدة (مثل LLVM أو WebAssembly) دون المساس بـ Lexer أو Parser.</li>
          </ul>
        </div>
      </div>
    </div>
    <div class="brand"><div class="brand-line"></div>BAYAN COMPILER<div class="brand-arr"></div></div>
    <div class="pn"><span class="n">13</span><span class="l">STRUCTURE</span></div>
  </section>
"""
slides_html.append(s13)

# ==================== SLIDE 14: TECH STACK ====================
s14 = """
  <!-- SLIDE 14: TECH STACK -->
  <section class="slide" id="s14">
    <div class="tl"></div>
    <div class="sc">
      <div class="slbl">TECH STACK &amp; TOOLS &bull; الشريحة 14</div>
      <h2 class="stitle">التقنيات، اللغات والأدوات المستخدمة</h2>
      <div class="ssub">منظومة تقنية حديثة بنيت بالكامل بأحدث إصدارات C# 12 ومنصة .NET 8</div>

      <div class="grid-2">
        <div>
          <h2 class="h">لغة البرمجة وبيئة التشغيل</h2>
          <table class="dt">
            <thead>
              <tr><th>المكون</th><th>الإصدار والخصائص المعتمدة</th></tr>
            </thead>
            <tbody>
              <tr><td><strong>C# 12</strong></td><td>اللغة الأساسية لكامل المشروع؛ استُفيد فيها من ميزات Pattern Matching المتقدمة لمطابقة عقد AST وبنى البيانات غير القابلة للتغيير (Records).</td></tr>
              <tr><td><strong>.NET 8 SDK</strong></td><td>إطار العمل الحديث عالي الأداء مع دعم متكامل لمعالجة نصوص Unicode واللغات المكتوبة من اليمين لليسار (RTL).</td></tr>
            </tbody>
          </table>

          <h2 class="h">أدوات توليد الكود والمحاكاة</h2>
          <table class="dt">
            <thead>
              <tr><th>الأداة</th><th>الدور في دورة الترجمة</th></tr>
            </thead>
            <tbody>
              <tr><td><strong>ilasm.exe</strong></td><td>مجمّع لغة CIL المدمج في .NET لإنتاج ملفات PE التنفيذية.</td></tr>
              <tr><td><strong>MARS Simulator</strong></td><td>محاكي معمارية MIPS لتشغيل وتحليل ملفات <code>output.asm</code>.</td></tr>
            </tbody>
          </table>
        </div>

        <div>
          <h2 class="h">بيئة التطوير وإدارة الشيفرة</h2>
          <ul class="bl">
            <li><strong>Visual Studio 2022 &amp; VS Code:</strong> أدوات التطوير وكتابة الكود وتصحيح الأخطاء.</li>
            <li><strong>Windows Forms:</strong> الواجهة الرسومية للـ IDE مع تخصيص كامل للخطوط والتخطيط العربي.</li>
            <li><strong>Git &amp; GitHub:</strong> إدارة الإصدارات والتنسيق بين أعضاء الفريق.</li>
            <li><strong>حزم الاختبارات الآلية (Unit Tests):</strong> التحقق الدوري من صحة كل مرحلة عبر حالات اختبار مؤتمتة تغطي القواعد والحالات الشاذة.</li>
          </ul>

          <div class="callout">
            <strong>صفر اعتماديات خارجية (Zero External Dependencies):</strong> لم يتم استخدام أي مكتبات خارجية لتوليد المحللات (مثل ANTLR أو Lex/Yacc)؛ بل كُتب الـ Lexer والـ Parser ومولدات الكود <strong>يدويًا من الصفر</strong> بنسبة 100%.
          </div>
        </div>
      </div>
    </div>
    <div class="brand"><div class="brand-line"></div>BAYAN COMPILER<div class="brand-arr"></div></div>
    <div class="pn"><span class="n">14</span><span class="l">TECH STACK</span></div>
  </section>
"""
slides_html.append(s14)

# ==================== SLIDE 15: CONCLUSION ====================
s15 = """
  <!-- SLIDE 15: CONCLUSION -->
  <section class="slide" id="s15">
    <div class="tl"></div>
    <div class="sc">
      <div class="slbl">CONCLUSION &amp; MILESTONES &bull; الشريحة 15</div>
      <h2 class="stitle">الخاتمة، حصاد الإنجازات والآفاق المستقبلية</h2>
      <div class="ssub">تطبيق عملي متكامل لنظريات بناء المترجمات يختتم بمنتج برمجي حقيقي وفعال</div>

      <div class="grid-2">
        <div>
          <h2 class="h">جدول الإنجازات المحققة بالمشروع</h2>
          <table class="dt">
            <thead>
              <tr><th>المرحلة</th><th>حالة الإنجاز</th><th>الملاحظات الفنية</th></tr>
            </thead>
            <tbody>
              <tr><td>التحليل المعجمي (Lexer)</td><td><span class="ok">✅ مكتمل 100%</span></td><td>94 رمزًا، دعم الهمزات والتطبيع الكامل</td></tr>
              <tr><td>التحليل النحوي (Parser)</td><td><span class="ok">✅ مكتمل 100%</span></td><td>محلل LL(1) مع شجرة AST كاملة</td></tr>
              <tr><td>التحليل الدلالي (Semantics)</td><td><span class="ok">✅ مكتمل 100%</span></td><td>فحص أنواع صارم، جدول رموز هرمي</td></tr>
              <tr><td>الكود الوسيط (TAC)</td><td><span class="ok">✅ مكتمل 100%</span></td><td>تبسيط التعبيرات وقواعد القفز المشروط</td></tr>
              <tr><td>لغة التجميع (MIPS)</td><td><span class="ok">✅ مكتمل 100%</span></td><td>توليد كود MARS متكامل وتفاعلي</td></tr>
              <tr><td>إنتاج الملف التنفيذي (.exe)</td><td><span class="ok">✅ مكتمل 100%</span></td><td>تجميع CIL وإنتاج EXE مستقل</td></tr>
              <tr><td>بيئة التطوير الرسومية (IDE)</td><td><span class="ok">✅ مكتمل 100%</span></td><td>محرر كامل، مفتش مراحل، وطرفية حية</td></tr>
            </tbody>
          </table>
        </div>

        <div>
          <h2 class="h">القيمة المكتسبة للمشروع</h2>
          <p>لم يكن المشروع مجرد تمرين أكاديمي، بل تجربة هندسية برمجية عميقة ربطت المفاهيم النظرية بالحوسبة التطبيقية؛ من هندسة اللغات الصورية إلى توليد التعليمات الآلية في الذاكرة.</p>

          <h2 class="h">الآفاق المستقبلية لتطوير «بيان»</h2>
          <ul class="bl">
            <li><strong>تحسين الكود (Optimization Passes):</strong> تطبيق خوارزميات إزالة الكود الميت و Constant Folding على الـ TAC.</li>
            <li><strong>دعم لغات أخرى:</strong> إضافة مولد كود لـ WebAssembly لتشغيل لغة بيان داخل المتصفح مباشرة.</li>
            <li><strong>توسيع مكتبة الدوال القياسية:</strong> إضافة دوال رياضية، ومعالجة ملفات مدمجة بلغة عربية.</li>
          </ul>

          <div class="callout" style="text-align:center; font-weight:600; font-size:14px; margin-top:16px;">
            نشكر لكم طيب المتابعة والاستماع، ونسعد بتلقي أسئلتكم واستفساراتكم.
          </div>
        </div>
      </div>
    </div>
    <div class="brand"><div class="brand-line"></div>BAYAN COMPILER<div class="brand-arr"></div></div>
    <div class="pn"><span class="n">15</span><span class="l">FINISH</span></div>
  </section>
"""
slides_html.append(s15)

footer_html = """
</div>

<script>
  // Simple navigation active state updater on scroll
  const slides = document.querySelectorAll('.slide');
  const dots = document.querySelectorAll('.dot');

  window.addEventListener('scroll', () => {
    let current = '';
    slides.forEach(slide => {
      const top = slide.offsetTop - 120;
      if (window.pageYOffset >= top) {
        current = slide.getAttribute('id');
      }
    });

    dots.forEach(dot => {
      dot.classList.remove('active');
      if (dot.getAttribute('href') === '#' + current) {
        dot.classList.add('active');
      }
    });
  });

  // Arrow key navigation between slides
  window.addEventListener('keydown', (e) => {
    if (e.key === 'ArrowDown' || e.key === 'PageDown' || e.key === 'ArrowLeft') {
      scrollNext();
    } else if (e.key === 'ArrowUp' || e.key === 'PageUp' || e.key === 'ArrowRight') {
      scrollPrev();
    }
  });

  function getCurrentIndex() {
    let idx = 0;
    slides.forEach((slide, i) => {
      if (window.pageYOffset >= slide.offsetTop - 180) {
        idx = i;
      }
    });
    return idx;
  }

  function scrollNext() {
    let idx = getCurrentIndex();
    if (idx < slides.length - 1) {
      slides[idx + 1].scrollIntoView({ behavior: 'smooth' });
    }
  }

  function scrollPrev() {
    let idx = getCurrentIndex();
    if (idx > 0) {
      slides[idx - 1].scrollIntoView({ behavior: 'smooth' });
    }
  }
</script>
</body>
</html>
"""

# Assemble everything
full_html_content = css + "".join(slides_html) + footer_html

with open(r'e:\BayanCompiler\presentation.html', 'w', encoding='utf-8') as f:
    f.write(full_html_content)

print(f"Successfully generated presentation.html with complete cover and RTL code blocks. Total size: {len(full_html_content)} bytes.")
