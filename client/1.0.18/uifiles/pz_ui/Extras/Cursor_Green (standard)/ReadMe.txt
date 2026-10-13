===== Information =====
Version 1.0.0
These instructions are based on the Default UI Files.
Do Not Copy & Paste, Manually Edit the XML File!
I recommend using a proper editor that can handle XML, such as notepad++
-->This will be using 2x for the examples, 
-->See specific folders for HotSpot/Size.

I made every TGA file 256x256 with the same file names.
This way if you want to change to a smaller or larger, 
size you only need to change the HotSpot/Size in the XML.

===== Custom Art =====
Don't like the art or how I scaled the image?
Edit the TGA files with your own art!
Keep in mind you may need to adjust the HotSpot/Size if you do this.

Want cursor art between sizes? example: 3.5x/2.5x/1.5x/etc
You can edit the art to be to your liking.
Just be aware of the HotSpot & Size.

===== Guide =====
== Step 0 ==
Copy the *.tga files from the folder scale of your choice,
 To the UI folder you plan to  use.
-->This will be using 2x for the examples, 
-->See specific folders for HotSpot/Size.

== Step 1 ==
Open EQUI_Animations.xml (NOT EQLSUI_Animations.xml)

== Step 2 ==
At the top below: <Schema xmlns = "EverQuestData" xmlns:dt = "EverQuestDataTypes"/>
 And above <TextureInfo item = "CS_Buttons.bmp">

____It will Look like This____
	<Schema xmlns = "EverQuestData" xmlns:dt = "EverQuestDataTypes"/>
	<TextureInfo item = "Custom_Cursor.tga">
		<Size>
			<CX>256</CX>
			<CY>256</CY>
		</Size>
	</TextureInfo>
	<TextureInfo item = "Custom_Cursor_Drag.tga">
		<Size>
			<CX>256</CX>
			<CY>256</CY>
		</Size>
	</TextureInfo>
	<TextureInfo item = "Custom_Cursor_Resize_EW.tga">
		<Size>
			<CX>256</CX>
			<CY>256</CY>
		</Size>
	</TextureInfo>
	<TextureInfo item = "Custom_Cursor_Resize_NESW.tga">
		<Size>
			<CX>256</CX>
			<CY>256</CY>
		</Size>
	</TextureInfo>
	<TextureInfo item = "Custom_Cursor_Resize_NS.tga">
		<Size>
			<CX>256</CX>
			<CY>256</CY>
		</Size>
	</TextureInfo>
	<TextureInfo item = "Custom_Cursor_Resize_NWSE.tga">
		<Size>
			<CX>256</CX>
			<CY>256</CY>
		</Size>
	</TextureInfo>
	<TextureInfo item = "CS_Buttons.bmp">

== Step 3 ==
Search for: A_DefaultCursor
____Set____
			<Texture>Custom_Cursor.tga</Texture>
			<Location>
				<X>0</X>
				<Y>0</Y>
			</Location>
			<Size>
				<CX>44</CX>
				<CY>44</CY>
			</Size>
			<Hotspot>
				<X>10</X>
				<Y>2</Y>
			</Hotspot>

== Step 4 ==
Search for: A_CursorResizeEW
____Set____
			<Texture>Custom_Cursor_Resize_EW.tga</Texture>
			<Location>
				<X>0</X>
				<Y>0</Y>
			</Location>
			<Size>
				<CX>44</CX>
				<CY>44</CY>
			</Size>
			<Hotspot>
				<X>22</X>
				<Y>22</Y>
			</Hotspot>

== Step 5 ==
Search for: A_CursorResizeNESW
____Set____
			<Texture>Custom_Cursor_Resize_NESW.tga</Texture>
			<Location>
				<X>0</X>
				<Y>0</Y>
			</Location>
			<Size>
				<CX>44</CX>
				<CY>44</CY>
			</Size>
			<Hotspot>
				<X>22</X>
				<Y>22</Y>
			</Hotspot>

== Step 6 ==
Search for: A_CursorResizeNS
____Set____
			<Texture>Custom_Cursor_Resize_NS.tga</Texture>
			<Location>
				<X>0</X>
				<Y>0</Y>
			</Location>
			<Size>
				<CX>44</CX>
				<CY>44</CY>
			</Size>
			<Hotspot>
				<X>22</X>
				<Y>22</Y>
			</Hotspot>

== Step 7 ==
Search for: A_CursorResizeNWSE
____Set____
			<Texture>Custom_Cursor_Resize_NWSE.tga</Texture>
			<Location>
				<X>0</X>
				<Y>0</Y>
			</Location>
			<Size>
				<CX>44</CX>
				<CY>44</CY>
			</Size>
			<Hotspot>
				<X>22</X>
				<Y>22</Y>
			</Hotspot>

== Step 8 ==
Search for: A_CursorDrag
____Set____
			<Texture>Custom_Cursor_Drag.tga</Texture>
			<Location>
				<X>0</X>
				<Y>0</Y>
			</Location>
			<Size>
				<CX>44</CX>
				<CY>44</CY>
			</Size>
			<Hotspot>
				<X>22</X>
				<Y>22</Y>
			</Hotspot>

== Step 9 ==
Make sure you have the correct UI selected
ReloadUI or restart client.
Enjoy & Leave Feedback!