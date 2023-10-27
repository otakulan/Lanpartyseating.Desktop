<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:wix="http://wixtoolset.org/schemas/v4/wxs">
    <xsl:output method="xml" indent="yes"/>

    <!-- Identity transformation: copy all content by default -->
    <xsl:template match="@*|node()">
        <xsl:copy>
            <xsl:apply-templates select="@*|node()"/>
        </xsl:copy>
    </xsl:template>

    <!-- Remove the executable, we reference it in the service installation -->
    <xsl:key
            name="FilesToRemove"
            match="wix:Component[wix:File[contains(@Source, 'Lanpartyseating.Desktop.exe')]]"
            use="@Id"
    />
    
    <!-- Remove the configuration files, they should be provided by the user -->
    <xsl:key
            name="FilesToRemove"
            match="wix:Component[wix:File[contains(@Source, 'appsettings.json')]]"
            use="@Id"
    />
    <xsl:key
            name="FilesToRemove"
            match="wix:Component[wix:File[contains(@Source, 'appsettings.Development.json')]]"
            use="@Id"
    />
    
    <!-- ...but if the element has the "FilesToRemove" key then don't render anything (i.e. removing it from the output) -->
    <xsl:template match="wix:ComponentRef[ key( 'FilesToRemove', @Id ) ]" />
    <xsl:template match="wix:Component[ key( 'FilesToRemove', @Id ) ]" />
</xsl:stylesheet>
