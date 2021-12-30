<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:msxsl="urn:schemas-microsoft-com:xslt" exclude-result-prefixes="msxsl"
>
  <xsl:output method="xml" indent="yes"/>

  <xsl:template match="@* | node()">
    <xsl:copy>
      <xsl:apply-templates select="@* | node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="StockQuote">
    <StockQuote>
      <xsl:attribute name="Date">
        <xsl:value-of select="@Date"/>
      </xsl:attribute>
      <xsl:attribute name="Close">
        <xsl:value-of select="@Close"/>
      </xsl:attribute>
    </StockQuote>
  </xsl:template>
</xsl:stylesheet>
